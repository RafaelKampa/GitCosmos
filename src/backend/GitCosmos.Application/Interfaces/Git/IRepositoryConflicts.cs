using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Interfaces.Git;
public record ConflictVersion(string Kind, string Text, string Sha, string Mode);
public record RepositoryConflict(string Path, string Operation, string Token, ConflictVersion Base, ConflictVersion Ours, ConflictVersion Theirs, ConflictVersion Working);
public record ConflictResolutionPlan(string Token, string Path, string Action, bool RemovesFile, string Kind, string Text);
public interface IRepositoryConflicts
{
    Task<RepositoryConflict> ReadAsync(string root, string path, CancellationToken cancellationToken);
    Task<ConflictResolutionPlan> PrepareAsync(string root, ResolveConflictRequest request, CancellationToken cancellationToken);
    Task ResolveAsync(string root, ResolveConflictRequest request, CancellationToken cancellationToken);
    Task ExternalAsync(string root, RepositoryConflictRequest request, CancellationToken cancellationToken);
}
