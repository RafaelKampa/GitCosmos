using System.Security.Cryptography;
using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryHistoryReader(IBoundedGitProcessRunner runner, IGitRepositoryInspector inspector) : IRepositoryHistoryReader
{
    private static readonly SemaphoreSlim Slots = new(2, 2);
    private const int MetadataLimit = 2_000_000;
    private const int DiffLimit = RepositoryChangesReader.MaximumDiffCharacters;

    private async Task<T> ReadAsync<T>(string path, Func<Task<T>> read, CancellationToken cancellationToken)
    {
        await Slots.WaitAsync(cancellationToken);
        try
        {
            var root = await inspector.InspectAsync(path, cancellationToken);
            if (!string.Equals(root.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase))
                throw new ValidationAppException(LocalizedText.Get("dashboard.root_changed"));
            return await read();
        }
        finally { Slots.Release(); }
    }

    private async Task<string> RunAsync(string path, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var result = await runner.RunLimitedAsync(path, args, MetadataLimit, cancellationToken);
        if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
        if (result.Truncated) throw new OperationFailedAppException(LocalizedText.Get("history.limit"));
        return result.Output;
    }

    private async Task<string> SnapshotAsync(string path, CancellationToken cancellationToken)
    {
        var refs = await RunAsync(path, ["for-each-ref", "--format=%(refname)%00%(objectname)%00"], cancellationToken);
        var head = await runner.RunLimitedAsync(path, ["rev-parse", "--verify", "--quiet", "HEAD"], 256, cancellationToken);
        if (head.Truncated || head.ExitCode is not (0 or 1)) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
        var symbolic = await runner.RunLimitedAsync(path, ["symbolic-ref", "--quiet", "HEAD"], 32768, cancellationToken);
        if (symbolic.Truncated || symbolic.ExitCode is not (0 or 1)) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refs + "\0HEAD\0" + head.Output + "\0" + symbolic.Output)));
    }

    public Task<GitHistoryPage> ReadAsync(string localPath, int page, int pageSize, string? snapshot, CancellationToken cancellationToken = default) =>
        ReadAsync(localPath, async () =>
        {
            var before = await SnapshotAsync(localPath, cancellationToken);
            if (snapshot != null && !string.Equals(before, snapshot, StringComparison.OrdinalIgnoreCase)) throw Changed();
            var output = await RunAsync(localPath,
                ["log", "--single-worktree", "--all", "--topo-order", "--no-color", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "--decorate=full", "-z",
                    "--format=" + GitHistoryParser.Format, "--skip=" + (page * pageSize), "--max-count=" + (pageSize + 1), "--"], cancellationToken);
            var commits = GitHistoryParser.ParseCommits(output);
            if (before != await SnapshotAsync(localPath, cancellationToken)) throw Changed();
            return new GitHistoryPage(commits.Take(pageSize).ToArray(), before, commits.Count > pageSize);
        }, cancellationToken);

    public Task<GitCommitDetails> ReadCommitAsync(string localPath, string sha, CancellationToken cancellationToken = default) =>
        ReadAsync(localPath, () => ReadCommitCoreAsync(localPath, sha, cancellationToken), cancellationToken);

    private async Task<GitCommitDetails> ReadCommitCoreAsync(string path, string sha, CancellationToken cancellationToken)
    {
        if (!GitHistoryParser.IsSha(sha)) throw new ValidationAppException(LocalizedText.Get("history.invalid"));
        var result = await runner.RunLimitedAsync(path,
            ["show", "--no-patch", "--no-color", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "--decorate=full", "-z", "--format=" + GitHistoryParser.Format, sha, "--"], MetadataLimit, cancellationToken);
        if (result.ExitCode != 0) throw new NotFoundAppException(LocalizedText.Get("history.not_found"));
        if (result.Truncated) throw new OperationFailedAppException(LocalizedText.Get("history.limit"));
        var commits = GitHistoryParser.ParseCommits(result.Output);
        if (commits.Count != 1 || !commits[0].Sha.Equals(sha, StringComparison.OrdinalIgnoreCase))
            throw new NotFoundAppException(LocalizedText.Get("history.not_found"));
        var commit = commits[0];
        var files = await RunAsync(path, [.. DiffArguments(commit), "--name-status", "-z", "--"], cancellationToken);
        return new(commit, GitHistoryParser.ParseFiles(files));
    }

    private static List<string> DiffArguments(GitCommit commit) =>
        ["--literal-pathspecs", "diff-tree", "--root", "--no-commit-id", "-r", "--no-ext-diff", "--no-textconv", "--no-color",
            "--no-relative", "--no-exit-code", "--no-quiet", "--submodule=short", "--find-renames",
            .. commit.Parents.Take(1), commit.Sha];

    public Task<GitFileDiff> ReadDiffAsync(string localPath, string sha, string path, CancellationToken cancellationToken = default) =>
        ReadAsync(localPath, async () =>
        {
            var details = await ReadCommitCoreAsync(localPath, sha, cancellationToken);
            var file = details.Files.FirstOrDefault(x => x.Path == path) ?? throw new NotFoundAppException(LocalizedText.Get("history.not_found"));
            // Only paths reported by this immutable commit are accepted. No filesystem reads.
            var paths = new List<string> { file.Path };
            if (file.OriginalPath != null) paths.Add(file.OriginalPath);
            var args = DiffArguments(details.Commit);
            var numbers = await runner.RunLimitedAsync(localPath, [.. args, "--numstat", "-z", "--", .. paths], DiffLimit, cancellationToken);
            if (numbers.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
            if (numbers.Truncated) return new GitFileDiff(path, "working", "tooLarge", "");
            if (numbers.Output.StartsWith("-\t-\t", StringComparison.Ordinal)) return new GitFileDiff(path, "working", "binary", "");
            var patch = await runner.RunLimitedAsync(localPath, [.. args, "--patch", "--unified=3", "--", .. paths], DiffLimit, cancellationToken);
            if (patch.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("history.failed"));
            if (patch.Truncated || patch.Output.Count(x => x == '\n') > 4000) return new GitFileDiff(path, "working", "tooLarge", "");
            return new GitFileDiff(path, "working", patch.Output.Length == 0 ? "empty" : "text", patch.Output);
        }, cancellationToken);

    private static ConflictAppException Changed() => new(LocalizedText.Get("history.changed"));
}
