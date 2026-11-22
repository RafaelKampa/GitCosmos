using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Repositories;

public class WorkspaceRepository(GitCosmosDbContext context) : Repository<Workspace>(context), IWorkspaceRepository
{
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        Context.Workspaces.AnyAsync(x => x.Id == id && x.Active, cancellationToken);
    public Task<bool> NameExistsAsync(string nameKey, CancellationToken cancellationToken = default) =>
        NameExistsAsync(nameKey, 0, cancellationToken);

    public Task<bool> NameExistsAsync(string nameKey, int excludingId, CancellationToken cancellationToken = default) =>
        Context.Workspaces.AnyAsync(x => x.Active && x.NameKey == nameKey && x.Id != excludingId, cancellationToken);

    public Task<Workspace?> GetFullByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Context.Workspaces.Include(x => x.Repositories).SingleOrDefaultAsync(x => x.Id == id && x.Active, cancellationToken);

    public async Task<int> UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        // GetFullByIdAsync returns a tracked entity; save only the changed properties.
        await Context.SaveChangesAsync(cancellationToken);
        return workspace.Id;
    }

    public async Task<int> DeleteAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        Context.WorkspaceRepositories.RemoveRange(workspace.Repositories);
        // The lifecycle change and link removals are committed in one transaction.
        await Context.SaveChangesAsync(cancellationToken);
        return workspace.Id;
    }

    public async Task<WorkspacePage> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Context.Workspaces.AsNoTracking().Where(x => x.Active);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.NameKey).ThenBy(x => x.Id)
            .Skip(page * pageSize).Take(pageSize).Include(x => x.Repositories).ToListAsync(cancellationToken);
        return new(items, total);
    }
}

