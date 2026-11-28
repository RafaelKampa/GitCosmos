using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;

public class RepositoryLineEditor(IBoundedGitProcessRunner reader, IGitBlobReader blobs, IGitMutationRunner writer,
    IGitRepositoryInspector inspector, IRepositoryChangesReader changes) : IRepositoryLineEditor
{
    private const int Limit = 200_000;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private record State(RepositoryLinesSnapshot View, string Baseline, string Mode, byte[]? Working = null);
    private async Task<string> Git(string root, IReadOnlyList<string> args, CancellationToken ct)
    {
        var result = await reader.RunLimitedAsync(root, args, 2_000_000, ct);
        if (result.ExitCode != 0 || result.Truncated) throw Failed();
        return result.Output;
    }
    private async Task<T> Locked<T>(string root, Func<Task<T>> action, CancellationToken ct)
    {
        var inspected = await inspector.InspectAsync(root, ct);
        if (!inspected.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var common = (await Git(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)).Trim();
        var gate = GitRepositoryMutationLock.For(common); await gate.WaitAsync(ct);
        try { return await action(); } finally { gate.Release(); }
    }
    private async Task<string> StoreBlob(string root, byte[] bytes, string? cleanPath, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), "gitcosmos-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true)) await stream.WriteAsync(bytes, ct);
            // --path applies Git's clean/eol attributes to untracked files; selected results are already in index format.
            var hash = await writer.RunAsync(root, ["hash-object", cleanPath is null ? "--no-filters" : "--path=" + cleanPath, "-w", "--", temp], ct);
            var oid = hash.Output.Trim(); if (hash.ExitCode != 0 || hash.Truncated || !GitHistoryParser.IsSha(oid)) throw Failed();
            return oid;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private async Task<State> Read(string root, RepositoryLinesRequest request, CancellationToken ct)
    {
        var full = CommitExplorer.SafePath(root, request.Path);
        var status = await changes.ReadAsync(root, ct);
        var file = status.Files.SingleOrDefault(file => file.Path == request.Path) ?? throw Stale();
        var gitDir = (await Git(root, ["rev-parse", "--absolute-git-dir"], ct)).Trim();
        var pending = new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG", "rebase-merge", "rebase-apply" }.Any(name => Path.Exists(Path.Combine(gitDir, name)));
        var index = await Git(root, ["ls-files", "--stage", "-z"], ct);
        var head = status.Status.HasCommits ? await Git(root, ["rev-parse", "HEAD"], ct) : "unborn";
        string mode = "100644"; string oid = ""; var unsupported = pending || status.Status.Conflicted > 0 || file.IsConflicted || file.IsSubmodule || file.OriginalPath is not null;
        foreach (var record in index.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t'); if (tab < 0) throw Invalid();
            if (record[(tab + 1)..] != request.Path) continue;
            var fields = record[..tab].Split(' ');
            if (fields.Length != 3 || !GitHistoryParser.IsSha(fields[1])) throw Invalid();
            mode = fields[0]; oid = fields[1]; unsupported |= fields[2] != "0" || mode is not ("100644" or "100755");
        }
        byte[] working = [];
        if (File.Exists(full))
        {
            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (stream.Length > Limit) return Unavailable("tooLarge");
            using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + count > Limit) return Unavailable("tooLarge"); output.Write(buffer, 0, count); }
            working = output.ToArray();
        }
        if (Directory.Exists(full)) unsupported = true;
        var baseBytes = oid.Length == 0 || unsupported ? [] : await blobs.ReadAsync(root, oid, ct);
        if (baseBytes.Length > Limit) return Unavailable("tooLarge");
        string baseline; string workingText;
        try { baseline = Utf8.GetString(baseBytes); workingText = Utf8.GetString(working); }
        catch (DecoderFallbackException) { return Unavailable("binary"); }
        if (baseBytes.Contains((byte)0) || working.Contains((byte)0)) return Unavailable("binary");
        var patch = "";
        if (!unsupported)
        {
            var relevant = request.Area == "staged" ? file.IndexStatus is not ("" or " " or "." or "?") : file.IsUntracked || file.WorkTreeStatus is not ("" or " " or ".");
            if (!relevant) throw Stale();
            if (file.IsUntracked)
            {
                // Git stores clean-filtered content. Only an unreachable object is created here, never an index entry.
                var cleanOid = await StoreBlob(root, working, request.Path, ct);
                var clean = await blobs.ReadAsync(root, cleanOid, ct);
                if (clean.Length > Limit) return Unavailable("tooLarge");
                try { workingText = Utf8.GetString(clean); } catch (DecoderFallbackException) { return Unavailable("binary"); }
                if (clean.Contains((byte)0)) return Unavailable("binary");
                patch = SelectedLinesBuilder.NewFilePatch(workingText);
            }
            else
            {
                var args = new List<string> { "--literal-pathspecs", "-c", "diff.external=", "diff", "--patch", "--no-ext-diff", "--no-textconv", "--no-color", "--no-relative", "--no-exit-code", "--no-quiet", "--no-renames", "--unified=3", "--diff-algorithm=myers", "--no-indent-heuristic", "--output-indicator-new=+", "--output-indicator-old=-", "--output-indicator-context= " };
                if (request.Area == "staged") args.Add("--cached");
                args.AddRange(["--", request.Path]); patch = await Git(root, args, ct);
            }
        }
        if (patch.Length > Limit || patch.Count(c => c == '\n') > 4000) return Unavailable("tooLarge");
        var metadata = patch.Split('\n').TakeWhile(line => !line.StartsWith("@@ ", StringComparison.Ordinal)).ToArray();
        if (metadata.Any(line => line.StartsWith("Binary files", StringComparison.Ordinal) || line == "GIT binary patch")) return Unavailable("binary");
        unsupported |= metadata.Any(line => line.StartsWith("old mode ", StringComparison.Ordinal)) || !patch.Contains("\n@@ ", StringComparison.Ordinal);
        // A staged deletion has no index entry: preserve its original regular-file mode when restoring selected lines.
        if (oid.Length == 0 && request.Area == "staged")
        {
            var deletedMode = patch.Split('\n').FirstOrDefault(line => line.StartsWith("deleted file mode ", StringComparison.Ordinal));
            if (deletedMode is not null) mode = deletedMode[18..];
            unsupported |= mode is not ("100644" or "100755");
        }
        var token = Hash(JsonSerializer.Serialize(new { root, request, index, head, patch, Working = Hash(working), Exists = File.Exists(full), pending, mode }));
        return new(new(new(request.Path, request.Area, "text", patch), token, !unsupported, unsupported ? LocalizedText.Get("lines.unsupported") : ""), baseline, mode, File.Exists(full) ? working : null);
        State Unavailable(string kind) => new(new(new(request.Path, request.Area, kind, ""), "", false, LocalizedText.Get("lines.unsupported")), "", mode);
    }
    private static RepositoryLinesPlan Plan(State state, PrepareLinesRequest request)
    {
        if (!state.View.CanSelect || state.View.Token != request.Token) throw Stale();
        var discard = request.Operation == "discard";
        if (request.Operation is not ("move" or "discard") || discard && request.File.Area != "working") throw Invalid();
        var reverse = request.File.Area == "staged" || discard;
        var baseline = discard ? SelectedLinesBuilder.Build(state.Baseline, state.View.Diff.Patch, SelectedLinesBuilder.ChangedLineIds(state.View.Diff.Patch), false) : state.Baseline;
        var result = SelectedLinesBuilder.Build(baseline, state.View.Diff.Patch, request.Lines, reverse);
        if (discard)
        {
            var working = Utf8.GetString(state.Working ?? []);
            if (working != baseline)
            {
                if (working.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n') || working.Replace("\r\n", "\n", StringComparison.Ordinal) != baseline) throw Invalid();
                result = result.Replace("\n", "\r\n", StringComparison.Ordinal);
            }
        }
        if (Utf8.GetByteCount(result) > Limit) throw Invalid();
        var remove = result.Length == 0 && state.View.Diff.Patch.Split('\n').Contains(reverse ? "--- /dev/null" : "+++ /dev/null");
        var token = Hash(JsonSerializer.Serialize(new { request.File, request.Operation, Snapshot = state.View.Token, Lines = request.Lines.Order().ToArray(), result, remove, state.Mode }));
        return new(token, request.File.Path, request.File.Area, request.Lines.Length, result, remove, request.Operation);
    }
    public Task<RepositoryLinesSnapshot> ReadAsync(string root, RepositoryLinesRequest request, CancellationToken ct) => Locked(root, async () => (await Read(root, request, ct)).View, ct);
    public Task<RepositoryLinesPlan> PrepareAsync(string root, PrepareLinesRequest request, CancellationToken ct) => Locked(root, async () =>
    {
        var state = await Read(root, request.File, ct); var plan = Plan(state, request);
        if (!request.ExportPatch) return plan;
        // Export the selected forward changes, including when the user is reviewing an Unstage operation.
        var forwardBase = request.File.Area == "staged" ? SelectedLinesBuilder.Build(state.Baseline, state.View.Diff.Patch, SelectedLinesBuilder.ChangedLineIds(state.View.Diff.Patch), true) : state.Baseline;
        var forward = SelectedLinesBuilder.Build(forwardBase, state.View.Diff.Patch, request.Lines, false);
        var patch = await SelectedPatchExport.Build(reader, root, request.File.Path, forwardBase, forward, state.View.Diff.Patch.Split('\n').Contains("--- /dev/null"), forward.Length == 0 && state.View.Diff.Patch.Split('\n').Contains("+++ /dev/null"), ct);
        return plan with { Patch = patch };
    }, ct);
    public Task ExecuteAsync(string root, ExecuteLinesRequest request, CancellationToken ct) => Locked(root, async () =>
    {
        var state = await Read(root, request.Request.File, ct); var plan = Plan(state, request.Request);
        if (plan.Token != request.Token) throw Stale();
        var path = request.Request.File.Path; string oid = "";
        if (plan.Operation == "discard")
        {
            var full = CommitExplorer.SafePath(root, path); ct.ThrowIfCancellationRequested();
            if (plan.RemovesEntry) { if ((await Read(root, request.Request.File, ct)).View.Token != state.View.Token) throw Stale(); ct.ThrowIfCancellationRequested(); File.Delete(CommitExplorer.SafePath(root, path)); }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!); CommitExplorer.SafePath(root, path);
                var temp = Path.Combine(Path.GetDirectoryName(full)!, ".gitcosmos-discard-" + Guid.NewGuid().ToString("N"));
                try { await File.WriteAllBytesAsync(temp, Utf8.GetBytes(plan.Result), ct); if ((await Read(root, request.Request.File, ct)).View.Token != state.View.Token) throw Stale(); ct.ThrowIfCancellationRequested(); CommitExplorer.SafePath(root, path); File.Move(temp, full, true); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return true;
        }
        if (!plan.RemovesEntry) oid = await StoreBlob(root, Utf8.GetBytes(plan.Result), null, ct);
        // External Git tools do not share our lock. Recheck after creating the blob, immediately before updating the index.
        if ((await Read(root, request.Request.File, ct)).View.Token != state.View.Token) throw Stale();
        ct.ThrowIfCancellationRequested(); CommitExplorer.SafePath(root, path);
        var result = await writer.RunAsync(root, plan.RemovesEntry ? ["update-index", "--force-remove", "--", path] : ["update-index", "--add", "--cacheinfo", state.Mode, oid, path], ct);
        if (result.ExitCode != 0 || result.Truncated) throw Failed();
        return true;
    }, ct);
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ValidationAppException Invalid() => new(LocalizedText.Get("lines.invalid"));
    private static ConflictAppException Stale() => new(LocalizedText.Get("lines.stale"));
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("lines.failed"));
}
