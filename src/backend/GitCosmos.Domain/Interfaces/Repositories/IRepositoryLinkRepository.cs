using GitCosmos.Domain.Entities;
using GitCosmos.Domain.ReadModels;

namespace GitCosmos.Domain.Interfaces.Repositories;

public interface IRepositoryLinkRepository
{
    Task<List<GitRepository>> GetAllAsync(int workspaceId, CancellationToken cancellationToken = default);
    Task<RepositoryLinkResult> AddAsync(int workspaceId, GitRepository candidate, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(int workspaceId, int repositoryId, CancellationToken cancellationToken = default);
}
