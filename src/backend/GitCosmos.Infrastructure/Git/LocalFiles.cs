using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Infrastructure.Git;
public class LocalFiles(IBoundedGitProcessRunner reader, IGitMutationRunner writer, IGitRepositoryInspector inspector,
    IRepositoryChangesReader changes, IGitBlobReader blobs, ICommitFileDesktop desktop, IWorkingFileEditors editors) : ILocalFiles
{
    private record State(string Token, GitChangesSnapshot Changes, Dictionary<string, byte[]?> Working, string Index, string Head, byte[]? Ignore);
    private async Task<string> Git(string root, IReadOnlyList<string> args, CancellationToken ct)
    {
        var output = await reader.RunLimitedAsync(root, args, 2_000_000, ct);
        if (output.Truncated || output.ExitCode != 0) throw Failed(); return output.Output;
    }
    private async Task<T> Locked<T>(string root, Func<Task<T>> action, CancellationToken ct)
    {
        var actual = await inspector.InspectAsync(root, ct);
        if (!actual.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var common = (await Git(root, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct)).Trim(); var gate = GitRepositoryMutationLock.For(common);
        await gate.WaitAsync(ct); try { return await action(); } finally { gate.Release(); }
    }
    private static async Task<byte[]?> Working(string root, string path, CancellationToken ct)
    {
        var full = CommitExplorer.SafePath(root, path); if (Directory.Exists(full)) throw Invalid(); if (!File.Exists(full)) return null;
        await using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        using var output = new MemoryStream(); var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer, ct)) > 0) { if (output.Length + count > 2_000_000) throw Invalid(); output.Write(buffer, 0, count); }
        return output.ToArray();
    }
    private async Task<State> Snapshot(string root, LocalFilesRequest request, bool mutable, CancellationToken ct)
    {
        var status = await changes.ReadAsync(root, ct); var working = new Dictionary<string, byte[]?>();
        foreach (var path in request.Paths)
        {
            var file = status.Files.SingleOrDefault(file => file.Path == path) ?? throw Stale();
            if (file.IsSubmodule || file.IsConflicted || file.OriginalPath is not null && mutable) throw Invalid();
            if (request.Area == "staged" ? file.IndexStatus == "." : file.WorkTreeStatus == ".") throw Stale();
            var full = CommitExplorer.SafePath(root, path); if (Directory.Exists(full)) throw Invalid();
            working[path] = mutable || request.Action is "patch" or "blame" or "lineHistory" or "diff" ? await Working(root, path, ct) : null;
            if (working.Values.Sum(bytes => bytes?.Length ?? 0) > 20_000_000) throw Invalid();
        }
        var index = await Git(root, ["ls-files", "--stage", "-z"], ct);
        foreach (var path in request.Paths) { var entry = Entry(index, path); if (entry is not null && entry.Value.Mode is not ("100644" or "100755")) throw Invalid(); }
        var head = status.Status.HasCommits ? (await Git(root, ["rev-parse", "HEAD"], ct)).Trim() : "";
        var gitDir = (await Git(root, ["rev-parse", "--absolute-git-dir"], ct)).Trim();
        var pending = new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG", "rebase-apply", "rebase-merge" }.Any(name => Path.Exists(Path.Combine(gitDir, name)));
        if (mutable && (pending || status.Status.Conflicted > 0)) throw Invalid();
        var ignore = request.Action == "ignore" ? await Working(root, ".gitignore", ct) : null;
        var token = Hash(JsonSerializer.Serialize(new { root, request.RepositoryId, request.WorkspaceId, request.Paths, request.Area, request.Action, request.Pattern, index, head, pending, status, Files = working.ToDictionary(pair => pair.Key, pair => pair.Value is null ? "absent" : Hash(pair.Value)), Ignore = ignore is null ? "absent" : Hash(ignore) }));
        return new(token, status, working, index, head, ignore);
    }
    private static (string Mode, string Oid)? Entry(string index, string path)
    {
        foreach (var row in index.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = row.IndexOf('\t'); if (tab < 0) throw Invalid(); if (row[(tab + 1)..] != path) continue;
            var fields = row[..tab].Split(' '); if (fields.Length != 3 || fields[2] != "0" || !GitHistoryParser.IsSha(fields[1])) throw Invalid(); return (fields[0], fields[1]);
        }
        return null;
    }
    private async Task<byte[]> Indexed(string root, State state, string path, CancellationToken ct) => Entry(state.Index, path) is { } entry ? await blobs.ReadAsync(root, entry.Oid, ct) : [];
    private static string IgnoreRules(State state, LocalFilesRequest request)
    {
        if (request.Area != "working" || request.Paths.Any(path => !state.Changes.Files.Single(file => file.Path == path).IsUntracked)) throw Invalid();
        static string Escape(string path) => Regex.Replace(path, @"([\\*?\[\]#! ])", @"\$1", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return string.Join('\n', request.Paths.Select(path => request.Pattern switch
        {
            "file" => "/" + Escape(path),
            "extension" when Path.GetExtension(path).Length > 1 => "*" + Escape(Path.GetExtension(path)),
            "folder" when path.Contains('/') => "/" + Escape(path[..path.LastIndexOf('/')]) + "/",
            _ => throw Invalid()
        }).Distinct()) + "\n";
    }
    private static LocalFilesPlan Plan(State state, LocalFilesRequest request)
    {
        if (request.Action is not ("discard" or "stash" or "ignore") || request.Action == "discard" && request.Area != "working" || request.Action == "stash" && state.Head.Length == 0) throw Invalid();
        var preview = request.Action == "ignore" ? IgnoreRules(state, request) : string.Join('\n', request.Paths);
        return new(Hash(state.Token + "\n" + preview), request.Action, request.Paths, preview);
    }
    public Task<LocalFilesPlan> PrepareAsync(string root, LocalFilesRequest request, CancellationToken ct) => Locked(root, async () => Plan(await Snapshot(root, request, true, ct), request), ct);
    public Task ExecuteAsync(string root, LocalFilesRequest request, CancellationToken ct) => Locked(root, async () =>
    {
        var state = await Snapshot(root, request, true, ct); var plan = Plan(state, request); if (plan.Token != request.Token) throw Stale();
        if ((await Snapshot(root, request, true, ct)).Token != state.Token) throw Stale(); ct.ThrowIfCancellationRequested();
        if (request.Action == "ignore")
        {
            var original = state.Ignore ?? []; var text = Text(original);
            await Save(root, ".gitignore", Encoding.UTF8.GetBytes(text + (text.Length > 0 && !text.EndsWith('\n') ? "\n" : "") + plan.Preview), state.Ignore, ct);
        }
        else if (request.Action == "stash") await Mutate(root, ["--literal-pathspecs", "stash", "push", "--include-untracked", "--message", "GitCosmos selected files", "--", .. request.Paths], ct);
        else
        {
            var tracked = request.Paths.Where(path => Entry(state.Index, path) is not null).ToArray();
            if (tracked.Length > 0) await Mutate(root, ["--literal-pathspecs", "restore", "--worktree", "--", .. tracked], ct);
            foreach (var path in request.Paths.Except(tracked)) { await VerifyWorking(root, path, state.Working[path], ct); ct.ThrowIfCancellationRequested(); File.Delete(CommitExplorer.SafePath(root, path)); }
        }
        return true;
    }, ct);
    private async Task Mutate(string root, IReadOnlyList<string> args, CancellationToken ct) { var result = await writer.RunAsync(root, args, ct); if (result.ExitCode != 0 || result.Truncated) throw Failed(); }
    private static async Task VerifyWorking(string root, string path, byte[]? expected, CancellationToken ct)
    {
        var actual = await Working(root, path, ct);
        if (actual is null ? expected is not null : expected is null || !actual.AsSpan().SequenceEqual(expected)) throw Stale();
    }
    private static async Task Save(string root, string path, byte[] bytes, byte[]? expected, CancellationToken ct)
    {
        var full = CommitExplorer.SafePath(root, path); var temp = Path.Combine(Path.GetDirectoryName(full)!, ".gitcosmos-local-" + Guid.NewGuid().ToString("N"));
        try { await File.WriteAllBytesAsync(temp, bytes, ct); await VerifyWorking(root, path, expected, ct); ct.ThrowIfCancellationRequested(); CommitExplorer.SafePath(root, path); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public Task DesktopAsync(string root, LocalFilesRequest request, CancellationToken ct) => Locked(root, async () =>
    {
        if (request.Paths.Length != 1) throw Invalid(); var state = await Snapshot(root, request, false, ct); var path = request.Paths[0]; var full = CommitExplorer.SafePath(root, path);
        switch (request.Action)
        {
            case "open": editors.OpenWith(full); break;
            case "reveal": desktop.Reveal(full); break;
            case "line": editors.RevealLine(full, request.Tool, request.Start); break;
            case "diff":
                var indexed = await Indexed(root, state, path, ct);
                var before = request.Area == "working" ? indexed : await HeadBytes(root, state, path, ct);
                var after = request.Area == "working" ? state.Working[path] ?? [] : indexed;
                desktop.Diff(request.Tool, Path.GetFileName(path), before, after); break;
            default: throw Invalid();
        }
        return true;
    }, ct);
    private async Task<byte[]> HeadBytes(string root, State state, string path, CancellationToken ct)
    {
        if (state.Head.Length == 0) return [];
        var tree = await Git(root, ["--literal-pathspecs", "ls-tree", "-z", state.Head, "--", path], ct);
        if (tree.Length == 0) return []; var tab = tree.IndexOf('\t'); if (tab < 0) throw Invalid(); var fields = tree[..tab].Split(' ');
        if (fields.Length != 3 || fields[0] is not ("100644" or "100755")) throw Invalid(); return await blobs.ReadAsync(root, fields[2], ct);
    }
    public Task<LocalFilesView> ReadAsync(string root, LocalFilesRequest request, CancellationToken ct) => Locked<LocalFilesView>(root, async () =>
    {
        if (request.Action is not ("info" or "patch" or "history" or "lineHistory" or "blame")) throw Invalid();
        var state = await Snapshot(root, request, false, ct); var path = request.Paths[0]; var full = CommitExplorer.SafePath(root, path);
        var patch = ""; IReadOnlyList<GitCommit> commits = []; IReadOnlyList<CommitBlameLine> blame = [];
        if (request.Action == "patch")
        {
            var parts = new List<string>();
            foreach (var selected in request.Paths)
            {
                var file = state.Changes.Files.Single(file => file.Path == selected);
                if (file.IsUntracked) parts.Add(await SelectedPatchExport.BuildBytes(reader, root, selected, [], state.Working[selected] ?? [], true, false, ct));
                else
                {
                    var diff = await Git(root, ["--literal-pathspecs", "diff", "--patch", "--binary", "--no-ext-diff", "--no-textconv", "--no-color", "--no-relative", .. (request.Area == "staged" ? new[] { "--cached" } : []), "--", selected], ct); parts.Add(diff);
                }
                if (parts.Sum(part => part.Length) > 2_000_000) throw Invalid();
            }
            patch = string.Concat(parts); if (patch.Length > 2_000_000) throw Invalid();
        }
        else if (state.Head.Length > 0 && request.Action == "history") commits = GitHistoryParser.ParseCommits(await Git(root, ["--literal-pathspecs", "log", "--follow", "--no-color", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "-z", "--format=" + GitHistoryParser.Format, "--max-count=101", state.Head, "--", path], ct));
        else if (state.Head.Length > 0 && request.Action is "blame" or "lineHistory")
        {
            var headBytes = await HeadBytes(root, state, path, ct);
            if (headBytes.Length > 0)
            {
                var content = request.Area == "staged" ? await Indexed(root, state, path, ct) : state.Working[path] ?? headBytes;
                var text = Text(content); if (text.Length > 200000) throw Invalid();
                var temp = Path.Combine(Path.GetTempPath(), "gitcosmos-blame-" + Guid.NewGuid().ToString("N")); string output;
                try { await File.WriteAllBytesAsync(temp, content, ct); output = await Git(root, ["--literal-pathspecs", "-c", "core.quotePath=false", "blame", "--line-porcelain", "--no-textconv", "--contents", temp, "--", path], ct); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                var parsed = ParseBlame(output); blame = parsed.Select(item => item.Line).ToArray();
                if (request.Action == "lineHistory")
                {
                    var groups = parsed.Where(item => item.Line.Line >= request.Start && item.Line.Line <= request.End && item.Line.Sha.Any(ch => ch != '0')).GroupBy(item => (item.Line.Sha, item.Path)).ToArray();
                    if (groups.Length > 16) throw Invalid(); var result = new List<GitCommit>();
                    foreach (var group in groups)
                    {
                        CommitExplorer.SafePath(root, group.Key.Path, false);
                        result.AddRange(GitHistoryParser.ParseCommits(await Git(root, ["log", "--no-color", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "-z", "--format=" + GitHistoryParser.Format, "--max-count=101", "--no-patch", "-L", $"{group.Min(item => item.Original)},{group.Max(item => item.Original)}:{group.Key.Path}", group.Key.Sha], ct)));
                    }
                    commits = result.DistinctBy(commit => commit.Sha).OrderByDescending(commit => commit.AuthoredAt).Take(101).ToArray();
                }
            }
        }
        return new(full, patch, commits, blame, desktop.Tools(), editors.Tools(path));
    }, ct);
    internal static string QuotePath(string path) => "\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal) + "\"";
    private sealed record Blamed(CommitBlameLine Line, int Original, string Path);
    private static List<Blamed> ParseBlame(string text)
    {
        var result = new List<Blamed>(); string sha = "", author = "", time = "", path = ""; int line = 0, original = 0;
        foreach (var row in text.Split('\n'))
        {
            var fields = row.Split(' ');
            if (fields.Length >= 3 && GitHistoryParser.IsSha(fields[0])) { sha = fields[0]; if (!int.TryParse(fields[1], out original) || !int.TryParse(fields[2], out line)) throw Invalid(); }
            else if (row.StartsWith("author ", StringComparison.Ordinal)) author = row[7..];
            else if (row.StartsWith("author-time ", StringComparison.Ordinal)) time = row[12..];
            else if (row.StartsWith("filename ", StringComparison.Ordinal)) { path = row[9..]; if (path.StartsWith('"')) { try { path = JsonSerializer.Deserialize<string>(path) ?? throw Invalid(); } catch (JsonException) { throw Invalid(); } } }
            else if (row.StartsWith('\t')) result.Add(new(new(line, sha, author, time, row[1..]), original, path));
        }
        return result;
    }
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private static string Text(byte[] bytes)
    {
        try { var text = new UTF8Encoding(false, true).GetString(bytes); return text.Contains('\0') ? throw Invalid() : text; }
        catch (DecoderFallbackException) { throw Invalid(); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static ValidationAppException Invalid() => new(LocalizedText.Get("local.invalid"));
    private static ConflictAppException Stale() => new(LocalizedText.Get("lines.stale"));
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("local.failed"));
}
