using GitCosmos.Domain.Entities;

namespace GitCosmos.Domain.Interfaces.Repositories;

public record RepositoryCatalog(List<Workspace> Workspaces, List<GitRepository> Repositories);
public record RegisteredRepository(GitRepository Repository, bool Created);
public interface IRepositoryCatalogRepository
{
    Task<RepositoryCatalog> GetAsync(CancellationToken cancellationToken = default);
    Task<GitRepository?> FindAsync(int id, CancellationToken cancellationToken = default);
    Task<RegisteredRepository> RegisterAsync(GitRepository candidate, CancellationToken cancellationToken = default);
}
