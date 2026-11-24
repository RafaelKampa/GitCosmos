using GitCosmos.Application.DTOs.Requests.Repository;

namespace GitCosmos.Application.Interfaces.Git;

public record RepositoryEditPlan(string Token, string Operation, IReadOnlyList<string> Paths, int FileCount,
    string Head, bool Amend, bool Published, string? PushDestination);
public record RepositoryEditResult(string Operation, string Head, bool Committed, bool PushAttempted, bool PushSucceeded, string? Warning);

public interface IRepositoryEditor
{
    Task<RepositoryEditPlan> PrepareAsync(string localPath, RepositoryEditRequest request, CancellationToken cancellationToken = default);
    Task<RepositoryEditResult> ExecuteAsync(string localPath, ExecuteRepositoryEditRequest request, CancellationToken cancellationToken = default);
}

public interface IGitMutationRunner
{
    Task<LimitedGitOutput> RunAsync(string localPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
