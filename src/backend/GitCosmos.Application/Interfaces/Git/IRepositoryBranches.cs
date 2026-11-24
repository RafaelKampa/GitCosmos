using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Interfaces.Git;

public record LocalGitBranch(string Name, string Sha, string? Upstream, bool IsCurrent, bool IsCheckedOut);
public record LocalGitBranches(string CurrentBranch, bool IsDetached, bool HasCommits, IReadOnlyList<LocalGitBranch> Branches);
public record RemoteGitBranch(string Name, string Sha, string Remote, string Branch);
public record BranchResources(string CurrentBranch, string OperationState, IReadOnlyList<string> Remotes, IReadOnlyList<RemoteGitBranch> Branches);
public record RebaseCommit(string Sha, string Subject);
public record RepositoryBranchPlan(string Token, string Operation, string Name, string NewName, string Sha)
{
    public string CurrentBranch { get; init; } = "";
    public string Target { get; init; } = "";
    public string Head { get; init; } = "";
    public string TargetSha { get; init; } = "";
    public string StateToken { get; init; } = "";
    public string? Url { get; init; }
    public bool RewritesHistory { get; init; }
    public bool Published { get; init; }
    public IReadOnlyList<RebaseCommit> Commits { get; init; } = [];
}
public interface IRepositoryBranches
{
    Task<LocalGitBranches> ReadAsync(string localPath, CancellationToken cancellationToken = default);
    Task<BranchResources> ReadResourcesAsync(string localPath, CancellationToken cancellationToken = default);
    Task<RepositoryBranchPlan> PrepareAsync(string localPath, RepositoryBranchRequest request, CancellationToken cancellationToken = default);
    Task<string> ExecuteAsync(string localPath, ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken = default);
}
