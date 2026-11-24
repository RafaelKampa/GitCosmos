using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class RepositoryBranches(IBoundedGitProcessRunner reader, IGitMutationRunner writer,
    IGitRepositoryInspector inspector, IRepositoryChangesReader changes) : IRepositoryBranches
{
    private readonly BranchOperationEngine _operations = new(reader, writer);
    public async Task<BranchResources> ReadResourcesAsync(string localPath, CancellationToken cancellationToken = default)
    {
        await ValidateRootAsync(localPath, cancellationToken);
        return await _operations.ResourcesAsync(localPath, cancellationToken);
    }
    private const string Format = "--format=%(refname)%00%(objectname)%00%(upstream)%00%(worktreepath)";
    private async Task<string> ReadGitAsync(string path, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var output = await reader.RunLimitedAsync(path, arguments, 1_000_000, cancellationToken);
        if (output.ExitCode != 0 || output.Truncated) throw Failed();
        return output.Output;
    }
    private async Task ValidateRootAsync(string path, CancellationToken cancellationToken)
    {
        var root = await inspector.InspectAsync(path, cancellationToken);
        if (!root.LocalPath.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException(LocalizedText.Get("dashboard.root_changed"));
    }
    public async Task<LocalGitBranches> ReadAsync(string localPath, CancellationToken cancellationToken = default)
    {
        await ValidateRootAsync(localPath, cancellationToken);
        var status = (await changes.ReadAsync(localPath, cancellationToken)).Status;
        var output = await ReadGitAsync(localPath, ["for-each-ref", "--sort=refname", Format, "refs/heads/"], cancellationToken);
        var branches = new List<LocalGitBranch>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            if (fields.Length != 4 || !fields[0].StartsWith("refs/heads/", StringComparison.Ordinal) || !GitHistoryParser.IsSha(fields[1])) throw Failed();
            var name = fields[0][11..];
            branches.Add(new(name, fields[1], fields[2].Length == 0 ? null : fields[2], !status.IsDetached && name == status.Branch, fields[3].Length > 0));
        }
        return new(status.Branch, status.IsDetached, status.HasCommits, branches);
    }
    private async Task<T> LockedAsync<T>(string path, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await ValidateRootAsync(path, cancellationToken);
        var common = (await ReadGitAsync(path, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken)).Trim();
        var gate = GitRepositoryMutationLock.For(common);
        await gate.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { gate.Release(); }
    }
    private async Task<RepositoryBranchPlan> PrepareCoreAsync(string path, RepositoryBranchRequest request, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(path, cancellationToken);
        if (BranchOperationEngine.Handles(request)) return await _operations.PrepareAsync(path, snapshot, request, cancellationToken);
        var source = snapshot.Branches.FirstOrDefault(branch => branch.Name == request.Name)
            ?? throw new ConflictAppException(LocalizedText.Get("branch.missing"));
        var gitDir = (await ReadGitAsync(path, ["rev-parse", "--absolute-git-dir"], cancellationToken)).Trim();
        if (new[] { "MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge", "rebase-apply", "BISECT_LOG" }.Any(name => Path.Exists(Path.Combine(gitDir, name))))
            throw new ConflictAppException(LocalizedText.Get("edit.in_progress"));
        if (request.Operation is "create" or "rename")
        {
            var valid = await reader.RunLimitedAsync(path, ["check-ref-format", "refs/heads/" + request.NewName], 256, cancellationToken);
            if (valid.Truncated || valid.ExitCode is not (0 or 1)) throw Failed();
            if (valid.ExitCode == 1) throw new ValidationAppException(LocalizedText.Get("branch.invalid"));
            if (snapshot.Branches.Any(branch => branch.Name.Equals(request.NewName, StringComparison.OrdinalIgnoreCase)))
                throw new ConflictAppException(LocalizedText.Get("branch.exists"));
        }
        if ((request.Operation == "delete" && source.IsCurrent) || (request.Operation is "rename" or "delete" && source.IsCheckedOut && !source.IsCurrent))
            throw new ConflictAppException(LocalizedText.Get("branch.checked_out"));
        var state = await ReadGitAsync(path, ["status", "--porcelain=v2", "-z", "--untracked-files=all"], cancellationToken);
        if (request.Operation == "checkout" && (source.IsCurrent || source.IsCheckedOut || state.Length > 0))
            throw new ConflictAppException(LocalizedText.Get("branch.checkout_blocked"));
        var head = snapshot.HasCommits ? (await ReadGitAsync(path, ["rev-parse", "--verify", "HEAD"], cancellationToken)).Trim() : "";
        if (request.Operation == "delete")
        {
            if (head.Length == 0) throw new ConflictAppException(LocalizedText.Get("branch.unmerged"));
            var merged = await reader.RunLimitedAsync(path, ["merge-base", "--is-ancestor", source.Sha, head], 256, cancellationToken);
            if (merged.Truncated || merged.ExitCode is not (0 or 1)) throw Failed();
            if (merged.ExitCode == 1) throw new ConflictAppException(LocalizedText.Get("branch.unmerged"));
        }
        var index = await ReadGitAsync(path, ["ls-files", "--stage", "-z"], cancellationToken);
        var stateToken = JsonSerializer.Serialize(new { Path = path, Request = request, Snapshot = snapshot, Head = head, State = state, Index = index });
        var token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stateToken)));
        return new(token, request.Operation, request.Name, request.NewName, source.Sha);
    }
    public Task<RepositoryBranchPlan> PrepareAsync(string localPath, RepositoryBranchRequest request, CancellationToken cancellationToken = default) =>
        LockedAsync(localPath, () => PrepareCoreAsync(localPath, request, cancellationToken), cancellationToken);
    public Task<string> ExecuteAsync(string localPath, ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken = default) =>
        LockedAsync(localPath, async () =>
        {
            var plan = await PrepareCoreAsync(localPath, request.Request, cancellationToken);
            if (!plan.Token.Equals(request.Token, StringComparison.Ordinal)) throw new ConflictAppException(LocalizedText.Get("edit.stale"));
            if (BranchOperationEngine.Handles(request.Request)) return await _operations.ExecuteAsync(localPath, request.Request, plan, cancellationToken);
            string[] args = plan.Operation switch
            {
                "create" => ["branch", "--no-track", "--no-recurse-submodules", "--", plan.NewName, plan.Sha],
                "rename" => ["branch", "-m", "--", plan.Name, plan.NewName],
                "delete" => ["branch", "-d", "--", plan.Name],
                "checkout" => ["switch", "--no-guess", "--no-recurse-submodules", "--no-overwrite-ignore", "--", plan.Name],
                _ => throw new ValidationAppException(LocalizedText.Get("branch.invalid"))
            };
            var result = await writer.RunAsync(localPath, args, cancellationToken);
            if (result.ExitCode != 0) throw Failed();
            return plan.Operation is "create" or "rename" ? plan.NewName : plan.Name;
        }, cancellationToken);
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("branch.failed"));
}
