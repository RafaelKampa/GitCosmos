using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryEditor(IBoundedGitProcessRunner reader, IGitMutationRunner writer,
    IGitRepositoryInspector inspector, IRepositoryChangesReader changes) : IRepositoryEditor
{
    // Bounded stripes serialize all worktrees sharing a Git common directory,
    // including access through different workspaces, without an unbounded map.
    private static readonly SemaphoreSlim[] Locks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private record Prepared(RepositoryEditPlan Plan, string? Remote, string? RemoteRef, string RemoteUrls);

    private async Task<T> LockedAsync<T>(string path, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        var root = await inspector.InspectAsync(path, cancellationToken);
        if (!root.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException(LocalizedText.Get("dashboard.root_changed"));
        var common = (await ReadAsync(path, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken)).Trim();
        var key = StringComparer.OrdinalIgnoreCase.GetHashCode(common) & int.MaxValue;
        var gate = Locks[key % Locks.Length];
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }

    private async Task<string> ReadAsync(string path, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var result = await reader.RunLimitedAsync(path, args, 4_000_000, cancellationToken);
        if (result.Truncated || result.ExitCode != 0) throw Failed();
        return result.Output;
    }

    private async Task<string> HeadAsync(string path, CancellationToken cancellationToken)
    {
        var result = await reader.RunLimitedAsync(path, ["rev-parse", "--verify", "--quiet", "HEAD"], 256, cancellationToken);
        if (result.ExitCode == 1 && !result.Truncated) return "";
        if (result.Truncated || result.ExitCode != 0 || !GitHistoryParser.IsSha(result.Output.Trim())) throw Failed();
        return result.Output.Trim();
    }

    private async Task<Prepared> PrepareCoreAsync(string path, RepositoryEditRequest request, CancellationToken cancellationToken)
    {
        var snapshot = await changes.ReadAsync(path, cancellationToken);
        var head = await HeadAsync(path, cancellationToken);
        var gitDir = (await ReadAsync(path, ["rev-parse", "--absolute-git-dir"], cancellationToken)).Trim();
        if (new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "BISECT_LOG" }.Any(name => Path.Exists(Path.Combine(gitDir, name))))
            throw new ConflictAppException(LocalizedText.Get("edit.in_progress"));
        if (snapshot.Files.Any(file => file.IsConflicted)) throw new ConflictAppException(LocalizedText.Get("edit.conflicts"));
        var staged = snapshot.Files.Where(file => file.IndexStatus != ".").ToArray();
        var selected = request.Operation == "commit" ? staged : request.Paths.Distinct(StringComparer.Ordinal).Select(selectedPath =>
            snapshot.Files.FirstOrDefault(file => file.Path == selectedPath && (request.Operation == "stage" ? file.WorkTreeStatus != "." : file.IndexStatus != "."))
                ?? throw new ConflictAppException(LocalizedText.Get("edit.stale"))).ToArray();
        if (selected.Length == 0 && !(request.Operation == "commit" && request.Amend && head.Length > 0))
            throw new ValidationAppException(LocalizedText.Get("edit.empty"));
        foreach (var file in selected)
        {
            if (file.IsSubmodule) throw new ValidationAppException(LocalizedText.Get("edit.invalid"));
            ValidatePath(path, file.Path);
            if (request.Operation == "stage" && Directory.Exists(Path.Combine(path, file.Path))) throw Invalid();
            if (file.OriginalPath is not null) ValidatePath(path, file.OriginalPath);
        }
        var paths = selected.SelectMany(file => file.OriginalPath is null || request.Operation == "stage" && file.WorkTreeStatus is not ("R" or "C")
            ? new[] { file.Path } : new[] { file.Path, file.OriginalPath }).Distinct(StringComparer.Ordinal).ToArray();
        if (paths.Sum(selectedPath => selectedPath.Length + 3) > 20000) throw Invalid();
        string? remote = null; string? remoteRef = null; string? destination = null; string remoteUrls = "";
        var published = false;
        if (request.Operation == "commit")
        {
            if (snapshot.Status.IsDetached || request.Amend && head.Length == 0) throw new ValidationAppException(LocalizedText.Get("edit.branch"));
            await ReadAsync(path, ["var", "GIT_AUTHOR_IDENT"], cancellationToken);
            await ReadAsync(path, ["var", "GIT_COMMITTER_IDENT"], cancellationToken);
            if (request.Amend)
                published = (await ReadAsync(path, ["for-each-ref", "--contains=" + head, "--format=%(refname)", "refs/remotes"], cancellationToken)).Trim().Length > 0;
            if (request.Push)
            {
                var upstream = (await ReadAsync(path, ["for-each-ref", "--format=%(upstream:remotename)%00%(upstream:remoteref)", "refs/heads/" + snapshot.Status.Branch], cancellationToken)).TrimEnd('\r', '\n').Split('\0');
                if (upstream.Length != 2 || string.IsNullOrWhiteSpace(upstream[0]) || upstream[0] == "." || upstream[0].StartsWith('-') || !upstream[1].StartsWith("refs/heads/", StringComparison.Ordinal))
                    throw new ValidationAppException(LocalizedText.Get("edit.upstream"));
                remote = upstream[0]; remoteRef = upstream[1]; destination = remote + "/" + remoteRef[11..];
                remoteUrls = await ReadAsync(path, ["remote", "get-url", "--push", "--all", remote], cancellationToken);
                if (remoteUrls.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length != 1) throw new ValidationAppException(LocalizedText.Get("edit.upstream"));
            }
        }
        var index = await ReadAsync(path, ["ls-files", "--stage", "-z"], cancellationToken);
        var state = JsonSerializer.Serialize(new { Path = path, Request = request, Head = head, snapshot.Status, snapshot.Files, Index = index, Published = published, Remote = remote, RemoteRef = remoteRef, RemoteUrls = remoteUrls });
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)));
        return new(new(token, request.Operation, paths, selected.Length, head, request.Amend, published, destination), remote, remoteRef, remoteUrls);
    }

    public Task<RepositoryEditPlan> PrepareAsync(string localPath, RepositoryEditRequest request, CancellationToken cancellationToken = default) =>
        LockedAsync(localPath, async () => (await PrepareCoreAsync(localPath, request, cancellationToken)).Plan, cancellationToken);

    public Task<RepositoryEditResult> ExecuteAsync(string localPath, ExecuteRepositoryEditRequest request, CancellationToken cancellationToken = default) =>
        LockedAsync(localPath, async () =>
        {
            var prepared = await PrepareCoreAsync(localPath, request.Request, cancellationToken);
            if (!prepared.Plan.Token.Equals(request.Token, StringComparison.Ordinal)) throw new ConflictAppException(LocalizedText.Get("edit.stale"));
            var action = request.Request;
            List<string> args = action.Operation switch
            {
                "stage" => ["--literal-pathspecs", "add", "--all", "--", .. prepared.Plan.Paths],
                "unstage" when prepared.Plan.Head.Length == 0 => ["--literal-pathspecs", "rm", "--cached", "--force", "--", .. prepared.Plan.Paths],
                "unstage" => ["--literal-pathspecs", "restore", "--staged", "--source=" + prepared.Plan.Head, "--", .. prepared.Plan.Paths],
                "commit" => ["commit", "-m", action.Subject.Trim()],
                _ => throw Invalid()
            };
            if (action.Operation == "commit")
            {
                if (!string.IsNullOrWhiteSpace(action.Description)) { args.Add("-m"); args.Add(action.Description); }
                if (action.Amend) args.Add("--amend");
            }
            var result = await writer.RunAsync(localPath, args, cancellationToken);
            if (result.ExitCode != 0) throw Failed();
            if (action.Operation != "commit") return new RepositoryEditResult(action.Operation, prepared.Plan.Head, false, false, false, null);
            // Once commit succeeds, report its identity even if push fails or is cancelled.
            var newHead = await HeadAsync(localPath, CancellationToken.None);
            if (!action.Push) return new RepositoryEditResult("commit", newHead, true, false, false, null);
            var pushed = false;
            try
            {
                if (!cancellationToken.IsCancellationRequested && prepared.RemoteUrls == await ReadAsync(localPath, ["remote", "get-url", "--push", "--all", prepared.Remote!], cancellationToken))
                    pushed = (await writer.RunAsync(localPath, ["push", "--no-force", "--no-mirror", "--no-follow-tags", "--recurse-submodules=no", "--", prepared.Remote!, newHead + ":" + prepared.RemoteRef], cancellationToken)).ExitCode == 0;
            }
            catch (Exception exception) when (exception is OperationCanceledException or OperationFailedAppException) { }
            return new RepositoryEditResult("commit", newHead, true, true, pushed, pushed ? null : LocalizedText.Get("edit.push_failed"));
        }, cancellationToken);

    private static void ValidatePath(string root, string path)
    {
        if (Path.IsPathRooted(path) || path.Contains(':') || path.Contains('\0')) throw Invalid();
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var current = root;
        foreach (var part in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar))
        {
            if (part.Equals(".git", StringComparison.OrdinalIgnoreCase)) throw Invalid();
            current = Path.Combine(current, part);
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        }
    }
    private static ValidationAppException Invalid() => new(LocalizedText.Get("edit.invalid"));
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("edit.failed"));
}
