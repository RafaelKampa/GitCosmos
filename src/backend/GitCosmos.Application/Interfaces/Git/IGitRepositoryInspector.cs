namespace GitCosmos.Application.Interfaces.Git;

public interface IGitRepositoryInspector
{
    Task<LocalGitRepository> InspectAsync(string localPath, CancellationToken cancellationToken = default);
}

public record LocalGitRepository(string Name, string LocalPath, string PathKey);
