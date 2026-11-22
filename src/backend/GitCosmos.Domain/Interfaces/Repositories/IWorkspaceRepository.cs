using GitCosmos.Domain.Entities;
using GitCosmos.Domain.ReadModels;

namespace GitCosmos.Domain.Interfaces.Repositories;

public interface IWorkspaceRepository : IRepository<Workspace>
{
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string nameKey, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string nameKey, int excludingId, CancellationToken cancellationToken = default);
    Task<Workspace?> GetFullByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task<int> DeleteAsync(Workspace workspace, CancellationToken cancellationToken = default);
    Task<WorkspacePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

