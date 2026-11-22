using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Repositories;

public class WorkspaceRepository(GitCosmosDbContext context) : Repository<Workspace>(context), IWorkspaceRepository
{
    public Task<bool> NameExistsAsync(string nameKey, CancellationToken cancellationToken = default) =>
        Context.Workspaces.AnyAsync(x => x.Active && x.NameKey == nameKey, cancellationToken);

    public async Task<WorkspacePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Context.Workspaces.AsNoTracking().Where(x => x.Active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.NameKey).ThenBy(x => x.Id)
            .Skip(page * pageSize).Take(pageSize).Include(x => x.Repositories).ToListAsync(cancellationToken);
        return new(items, total);
    }
}

