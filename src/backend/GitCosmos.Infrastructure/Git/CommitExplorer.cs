using GitCosmos.Application.DTOs.Requests.Repository;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;

public class CommitExplorer(IBoundedGitProcessRunner runner, IGitRepositoryInspector inspector, IRepositoryHistoryReader history, IGitBlobReader blobs, ICommitFileDesktop desktop) : ICommitExplorer
{
    private static readonly SemaphoreSlim ReadSlots = new(2, 2);
    private async Task CheckAsync(string root, string sha, CancellationToken ct)
    {
        if (!GitHistoryParser.IsSha(sha)) throw Invalid();
        var inspected = await inspector.InspectAsync(root, ct);
        if (!inspected.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var resolved = await ReadAsync(root, ["rev-parse", "--verify", "--end-of-options", sha + "^{commit}"], ct);
        if (!resolved.Trim().Equals(sha, StringComparison.OrdinalIgnoreCase)) throw Invalid();
    }
    private async Task<string> ReadAsync(string root, IReadOnlyList<string> args, CancellationToken ct)
    {
        await ReadSlots.WaitAsync(ct);
        try
        {
            var output = await runner.RunLimitedAsync(root, args, 2_000_000, ct);
            if (output.Truncated) throw new ValidationAppException(LocalizedText.Get("commit.limit"));
            if (output.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
            return output.Output;
        }
        finally { ReadSlots.Release(); }
    }
    public async Task<IReadOnlyList<CommitTreeEntry>> TreeAsync(string root, string sha, CancellationToken ct)
    {
        await CheckAsync(root, sha, ct);
        var output = await ReadAsync(root, ["ls-tree", "-r", "-z", "--full-tree", sha], ct);
        var entries = new List<CommitTreeEntry>();
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t'); if (tab < 0) throw Invalid();
            var fields = record[..tab].Split(' ');
            if (fields.Length != 3 || !GitHistoryParser.IsSha(fields[2]) || fields[0] is not ("100644" or "100755" or "120000" or "160000")) throw Invalid();
            entries.Add(new(record[(tab + 1)..], fields[0], fields[2]));
        }
        if (entries.Count > 10000) throw new ValidationAppException(LocalizedText.Get("commit.limit"));
        return entries;
    }
    private async Task<CommitTreeEntry?> EntryAsync(string root, string sha, string path, CancellationToken ct) => (await TreeAsync(root, sha, ct)).FirstOrDefault(entry => entry.Path == path);
    private async Task<byte[]> BytesAsync(string root, CommitTreeEntry entry, CancellationToken ct)
    {
        if (entry.Mode == "160000") throw new ValidationAppException(LocalizedText.Get("commit.submodule"));
        return await blobs.ReadAsync(root, entry.Sha, ct);
    }
    public async Task<CommitFileContent> FileAsync(string root, string sha, string path, CancellationToken ct)
    {
        var entry = await EntryAsync(root, sha, path, ct) ?? throw Missing();
        if (entry.Mode == "160000") return new(path, "submodule", entry.Sha, "", SafePath(root, path, false));
        var bytes = await BytesAsync(root, entry, ct); string text; string kind;
        try { text = new UTF8Encoding(false, true).GetString(bytes); kind = bytes.Contains((byte)0) ? "binary" : "text"; }
        catch (DecoderFallbackException) { text = ""; kind = "binary"; }
        // Full paths are only used for native operations after additional filesystem checks.
        var full = SafePath(root, path, false);
        return new(path, kind, kind == "text" ? text : "", Convert.ToBase64String(bytes), full);
    }
    private async Task<string> ExistingPathAsync(string root, string sha, string path, CancellationToken ct)
    {
        if (await EntryAsync(root, sha, path, ct) is not null) return path;
        var commit = await history.ReadCommitAsync(root, sha, ct);
        return commit.Files.Any(file => file.Path == path) ? path : throw Missing();
    }
    public async Task<IReadOnlyList<GitCommit>> HistoryAsync(string root, string sha, string path, CancellationToken ct)
    {
        await ExistingPathAsync(root, sha, path, ct);
        var output = await ReadAsync(root, ["--literal-pathspecs", "log", "--follow", "--no-color", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "--decorate=full", "-z", "--format=" + GitHistoryParser.Format, "--max-count=101", sha, "--", path], ct);
        return GitHistoryParser.ParseCommits(output);
    }
    public async Task<IReadOnlyList<CommitBlameLine>> BlameAsync(string root, string sha, string path, CancellationToken ct)
    {
        var content = await FileAsync(root, sha, path, ct);
        if (content.Kind != "text") throw new ValidationAppException(LocalizedText.Get("commit.text_only"));
        var output = await ReadAsync(root, ["--literal-pathspecs", "-c", "core.quotePath=false", "blame", "--line-porcelain", "--no-textconv", sha, "--", path], ct);
        var lines = new List<CommitBlameLine>(); string oid = "", author = "", time = ""; var lineNumber = 0;
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split(' ');
            if (parts.Length >= 3 && GitHistoryParser.IsSha(parts[0])) { oid = parts[0]; lineNumber = int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture); }
            else if (line.StartsWith("author ", StringComparison.Ordinal)) author = line[7..];
            else if (line.StartsWith("author-time ", StringComparison.Ordinal)) time = line[12..];
            else if (line.StartsWith('\t')) lines.Add(new(lineNumber, oid, author, time, line[1..]));
        }
        return lines;
    }
    private async Task<(CommitRestorePlan Plan, byte[]? Content)> PrepareCoreAsync(string root, CommitRestoreRequest request, CancellationToken ct)
    {
        await CheckAsync(root, request.File.Sha, ct);
        var details = await history.ReadCommitAsync(root, request.File.Sha, ct);
        var file = details.Files.FirstOrDefault(file => file.Path == request.File.Path) ?? throw Missing();
        var source = request.Before ? details.Commit.Parents.FirstOrDefault() : request.File.Sha;
        var sourcePath = request.Before ? file.OriginalPath ?? file.Path : file.Path;
        var entry = source is null ? null : await EntryAsync(root, source, sourcePath, ct);
        if (entry?.Mode is "120000" or "160000") throw Invalid();
        var bytes = entry is null ? null : await BytesAsync(root, entry, ct);
        var full = SafePath(root, file.Path);
        if (Directory.Exists(full)) throw Invalid();
        var gitDir = (await ReadAsync(root, ["rev-parse", "--absolute-git-dir"], ct)).Trim();
        if (new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "BISECT_LOG" }.Any(name => Path.Exists(Path.Combine(gitDir, name)))) throw new ConflictAppException(LocalizedText.Get("edit.in_progress"));
        var status = await ReadAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], ct);
        var index = await ReadAsync(root, ["ls-files", "--stage", "-z"], ct);
        var head = await ReadAsync(root, ["rev-parse", "HEAD"], ct);
        var current = "missing";
        if (File.Exists(full))
        {
            if (new FileInfo(full).Length > 8_000_000) throw new ValidationAppException(LocalizedText.Get("commit.limit"));
            current = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(full, ct)));
        }
        var sourceHash = bytes is null ? "absent" : Convert.ToHexString(SHA256.HashData(bytes));
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { root, request.File, request.Before, source, sourcePath, sourceHash, SourceObject = entry?.Sha, status, index, head, current }))));
        return (new(token, file.Path, source ?? "", sourcePath, entry is not null), bytes);
    }
    private async Task<T> LockedAsync<T>(string root, Func<Task<T>> action, CancellationToken ct)
    {
        var common = (await ReadAsync(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)).Trim();
        var gate = GitRepositoryMutationLock.For(common); await gate.WaitAsync(ct);
        try { return await action(); } finally { gate.Release(); }
    }
    public Task<CommitRestorePlan> PrepareAsync(string root, CommitRestoreRequest request, CancellationToken ct) => LockedAsync(root, async () => (await PrepareCoreAsync(root, request, ct)).Plan, ct);
    public Task RestoreAsync(string root, CommitRestoreRequest request, CancellationToken ct) => LockedAsync(root, async () =>
    {
        var prepared = await PrepareCoreAsync(root, request, ct);
        if (!prepared.Plan.Token.Equals(request.Token, StringComparison.Ordinal)) throw new ConflictAppException(LocalizedText.Get("edit.stale"));
        var full = SafePath(root, request.File.Path);
        ct.ThrowIfCancellationRequested();
        if (prepared.Content is null) File.Delete(full);
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!); SafePath(root, request.File.Path);
            var temporary = Path.Combine(Path.GetDirectoryName(full)!, ".gitcosmos-restore-" + Guid.NewGuid().ToString("N"));
            try
            {
                await File.WriteAllBytesAsync(temporary, prepared.Content, ct);
                ct.ThrowIfCancellationRequested(); SafePath(root, request.File.Path);
                File.Move(temporary, full, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        return true;
    }, ct);
    public IReadOnlyList<CommitExternalTool> Tools() => desktop.Tools();
    public async Task DesktopAsync(string root, CommitDesktopRequest request, CancellationToken ct)
    {
        await ExistingPathAsync(root, request.File.Sha, request.File.Path, ct);
        if (request.Action == "reveal") { desktop.Reveal(SafePath(root, request.File.Path)); return; }
        var details = await history.ReadCommitAsync(root, request.File.Sha, ct);
        var file = details.Files.FirstOrDefault(file => file.Path == request.File.Path);
        var entry = await EntryAsync(root, request.File.Sha, request.File.Path, ct);
        var after = entry is null ? [] : await BytesAsync(root, entry, ct);
        if (request.Action == "open") { if (entry is null) throw Missing(); desktop.Open(Path.GetFileName(request.File.Path), after); return; }
        if (request.Action != "diff" || file is null) throw Invalid();
        var parent = details.Commit.Parents.FirstOrDefault();
        var beforeEntry = parent is null ? null : await EntryAsync(root, parent, file.OriginalPath ?? file.Path, ct);
        var before = beforeEntry is null ? [] : await BytesAsync(root, beforeEntry, ct);
        desktop.Diff(request.Tool, Path.GetFileName(request.File.Path), before, after);
    }
    internal static string SafePath(string root, string path, bool inspectFileSystem = true)
    {
        if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\\') || path.Contains('\0') || path.Split('/').Any(part => part is "" or "." or ".." || part.TrimEnd('.', ' ').Equals(".git", StringComparison.OrdinalIgnoreCase))) throw Invalid();
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        if (inspectFileSystem)
        {
            var current = root;
            foreach (var part in path.Split('/')) { current = Path.Combine(current, part); if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Invalid(); }
        }
        return full;
    }
    private static ValidationAppException Invalid() => new(LocalizedText.Get("changes.invalid_selection"));
    private static NotFoundAppException Missing() => new(LocalizedText.Get("history.not_found"));
}
