using GitCosmos.Domain.Entities;
using GitCosmos.Domain.ReadModels;

namespace GitCosmos.Domain.Interfaces.Repositories;

public interface IWorkspaceRepository : IRepository<Workspace>
{
    Task<bool> NameExistsAsync(string nameKey, CancellationToken cancellationToken = default);
    Task<WorkspacePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

