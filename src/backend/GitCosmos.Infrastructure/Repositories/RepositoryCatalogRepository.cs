using System.Data;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Repositories;

public class RepositoryCatalogRepository(GitCosmosDbContext context) : IRepositoryCatalogRepository
{
    public async Task<RepositoryCatalog> GetAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = await context.Workspaces.AsNoTracking().Where(x => x.Active).OrderBy(x => x.NameKey).ToListAsync(cancellationToken);
        var repositories = await context.GitRepositories.AsNoTracking().Include(x => x.Workspaces.Where(w => w.Workspace.Active))
            .OrderBy(x => x.Name).ThenBy(x => x.PathKey).ToListAsync(cancellationToken);
        return new(workspaces, repositories);
    }
    public Task<GitRepository?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        context.GitRepositories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<RegisteredRepository> RegisterAsync(GitRepository candidate, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await context.GitRepositories.SingleOrDefaultAsync(x => x.PathKey == candidate.PathKey, cancellationToken);
        if (existing != null) return new(existing, false);
        context.GitRepositories.Add(candidate);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(candidate, true);
    }
}
