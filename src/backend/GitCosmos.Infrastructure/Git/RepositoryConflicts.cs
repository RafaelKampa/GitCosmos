using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;

public class RepositoryConflicts(IBoundedGitProcessRunner reader, IGitBlobReader blobs, IGitMutationRunner writer, IGitRepositoryInspector inspector, ICommitFileDesktop desktop) : IRepositoryConflicts
{
    private const int Limit = 200_000;
    private record State(RepositoryConflict View, string FullPath, byte[]? Working, Dictionary<int, (ConflictVersion View, byte[] Bytes)> Stages);
    private record Prepared(ConflictResolutionPlan Plan, byte[]? Bytes);
    private static readonly Regex Markers = new(@"(?m)^(<{7,}(?: |\r?$)|={7,}\r?$|>{7,}(?: |\r?$)|\|{7,}(?: |\r?$))", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private async Task<string> ReadGitAsync(string root, IReadOnlyList<string> args, CancellationToken ct)
    {
        var output = await reader.RunLimitedAsync(root, args, 2_000_000, ct);
        if (output.Truncated || output.ExitCode != 0) throw Failed();
        return output.Output;
    }
    private async Task<T> LockedAsync<T>(string root, Func<Task<T>> action, CancellationToken ct)
    {
        var inspected = await inspector.InspectAsync(root, ct);
        if (!inspected.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var common = (await ReadGitAsync(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)).Trim();
        var gate = GitRepositoryMutationLock.For(common); await gate.WaitAsync(ct);
        try { return await action(); } finally { gate.Release(); }
    }
    private static ConflictVersion Describe(byte[]? bytes, string sha = "", string mode = "")
    {
        if (bytes is null) return new("absent", "", "", "");
        if (bytes.Length > Limit) return new("tooLarge", "", sha, mode);
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return bytes.Contains((byte)0) ? new("binary", "", sha, mode) : new("text", text, sha, mode);
        }
        catch (DecoderFallbackException) { return new("binary", "", sha, mode); }
    }
    private async Task<State> ReadCoreAsync(string root, string path, CancellationToken ct)
    {
        var full = CommitExplorer.SafePath(root, path);
        var gitDir = (await ReadGitAsync(root, ["rev-parse", "--absolute-git-dir"], ct)).Trim();
        if (new[] { "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG" }.Any(name => Path.Exists(Path.Combine(gitDir, name)))) throw new ConflictAppException(LocalizedText.Get("conflict.operation"));
        var operation = Path.Exists(Path.Combine(gitDir, "rebase-merge")) || Path.Exists(Path.Combine(gitDir, "rebase-apply")) ? "rebase" : Path.Exists(Path.Combine(gitDir, "MERGE_HEAD")) ? "merge" : "";
        var index = await ReadGitAsync(root, ["ls-files", "--stage", "-z"], ct);
        var unmerged = await ReadGitAsync(root, ["--literal-pathspecs", "ls-files", "--unmerged", "-z", "--", path], ct);
        var stages = new Dictionary<int, (ConflictVersion, byte[])>();
        foreach (var record in unmerged.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = record.IndexOf('\t'); if (tab < 0 || record[(tab + 1)..] != path) throw Invalid();
            var fields = record[..tab].Split(' ');
            if (fields.Length != 3 || fields[0] is not ("100644" or "100755") || !GitHistoryParser.IsSha(fields[1]) || !int.TryParse(fields[2], out var stage) || stage is < 1 or > 3 || stages.ContainsKey(stage)) throw Invalid();
            var bytes = await blobs.ReadAsync(root, fields[1], ct);
            stages.Add(stage, (Describe(bytes, fields[1], fields[0]), bytes));
        }
        if (stages.Count == 0) throw Stale();
        if (Directory.Exists(full)) throw Invalid();
        byte[]? working = null;
        if (File.Exists(full))
        {
            await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (stream.Length > GitBlobReader.MaximumBytes) throw new ValidationAppException(LocalizedText.Get("conflict.limit"));
            using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + count > GitBlobReader.MaximumBytes) throw Invalid(); output.Write(buffer, 0, count); }
            working = output.ToArray();
        }
        var head = await ReadGitAsync(root, ["rev-parse", "HEAD"], ct);
        var operationFiles = new Dictionary<string, string>();
        foreach (var name in new[] { "MERGE_HEAD", "MERGE_MSG", "rebase-merge/head-name", "rebase-merge/onto", "rebase-merge/msgnum", "rebase-merge/git-rebase-todo", "rebase-apply/head-name", "rebase-apply/onto", "rebase-apply/next" })
        {
            var file = new FileInfo(Path.Combine(gitDir, name));
            if (!file.Exists) continue;
            if (file.Length > 200_000 || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Invalid();
            operationFiles[name] = Hash(await File.ReadAllBytesAsync(file.FullName, ct));
        }
        var token = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { root, path, index, unmerged, head, operation, operationFiles, Working = working is null ? "absent" : Hash(working) })));
        ConflictVersion Version(int stage) => stages.TryGetValue(stage, out var value) ? value.Item1 : Describe(null);
        return new(new(path, operation, token, Version(1), Version(2), Version(3), Describe(working)), full, working, stages);
    }
    public Task<RepositoryConflict> ReadAsync(string root, string path, CancellationToken ct) => LockedAsync(root, async () => (await ReadCoreAsync(root, path, ct)).View, ct);
    private static Prepared Prepare(State state, ResolveConflictRequest request)
    {
        byte[]? bytes = request.Action switch
        {
            "delete" => null,
            "stage" => state.Working,
            "edit" when state.View.Working.Kind is "text" or "absent" => Encoding.UTF8.GetBytes(request.Content),
            "ours" => state.Stages.TryGetValue(2, out var ours) ? ours.Bytes : null,
            "theirs" => state.Stages.TryGetValue(3, out var theirs) ? theirs.Bytes : null,
            _ => throw Invalid()
        };
        var version = Describe(bytes);
        if (request.Action == "edit" && bytes!.Length > Limit) throw Invalid();
        if (version.Kind == "text" && Markers.IsMatch(version.Text)) throw new ValidationAppException(LocalizedText.Get("conflict.markers"));
        var token = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { State = state.View.Token, request.File, request.Action, Content = bytes is null ? "absent" : Hash(bytes) })));
        return new(new(token, state.View.Path, request.Action, bytes is null, version.Kind, version.Text), bytes);
    }
    public Task<ConflictResolutionPlan> PrepareAsync(string root, ResolveConflictRequest request, CancellationToken ct) => LockedAsync(root, async () =>
    {
        var state = await ReadCoreAsync(root, request.File.Path, ct);
        if (request.Token != state.View.Token) throw Stale();
        return Prepare(state, request).Plan;
    }, ct);
    public Task ResolveAsync(string root, ResolveConflictRequest request, CancellationToken ct) => LockedAsync(root, async () =>
    {
        var state = await ReadCoreAsync(root, request.File.Path, ct); var prepared = Prepare(state, request);
        if (request.Token != prepared.Plan.Token) throw Stale();
        ct.ThrowIfCancellationRequested(); var full = CommitExplorer.SafePath(root, request.File.Path);
        if (request.Action != "stage")
        {
            if (prepared.Bytes is null) File.Delete(full);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!); CommitExplorer.SafePath(root, request.File.Path);
                var temp = Path.Combine(Path.GetDirectoryName(full)!, ".gitcosmos-resolution-" + Guid.NewGuid().ToString("N"));
                try
                {
                    await File.WriteAllBytesAsync(temp, prepared.Bytes, ct); ct.ThrowIfCancellationRequested(); CommitExplorer.SafePath(root, request.File.Path);
                    File.Move(temp, full, overwrite: true);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        // A failed/cancelled add may leave a saved resolution: never retry or roll back automatically.
        var result = await writer.RunAsync(root, ["--literal-pathspecs", "add", "--all", "--", request.File.Path], ct);
        if (result.ExitCode != 0) throw Failed();
        var remaining = await ReadGitAsync(root, ["--literal-pathspecs", "ls-files", "--unmerged", "-z", "--", request.File.Path], ct);
        if (remaining.Length > 0) throw Failed();
        return true;
    }, ct);
    public Task ExternalAsync(string root, RepositoryConflictRequest request, CancellationToken ct) => LockedAsync(root, async () =>
    {
        var state = await ReadCoreAsync(root, request.Path, ct);
        if (state.Working is null) throw Invalid();
        desktop.Edit(state.FullPath); return true;
    }, ct);
    private static ValidationAppException Invalid() => new(LocalizedText.Get("conflict.invalid"));
    private static ConflictAppException Stale() => new(LocalizedText.Get("conflict.stale"));
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("conflict.failed"));
}
