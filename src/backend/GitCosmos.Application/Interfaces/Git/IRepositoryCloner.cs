namespace GitCosmos.Application.Interfaces.Git;

public record RepositoryClonePlan(string RemoteUrl, string ParentPath, string FolderName, string DestinationPath);

public interface IRepositoryCloner
{
    RepositoryClonePlan Plan(string remoteUrl, string parentPath);
    Task<LocalGitRepository> CloneAsync(RepositoryClonePlan plan, CancellationToken cancellationToken = default);
}

public interface IGitCloneRunner
{
    Task<GitCommandResult> CloneAsync(string remoteUrl, string destinationPath, CancellationToken cancellationToken = default);
}
