using System.Data;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Repositories;

public class RepositoryLinkRepository(GitCosmosDbContext context) : IRepositoryLinkRepository
{
    public Task<List<GitRepository>> GetAllAsync(int workspaceId, CancellationToken cancellationToken = default) =>
        context.WorkspaceRepositories.AsNoTracking()
            .Where(x => x.WorkspaceId == workspaceId && x.Workspace.Active)
            .Select(x => x.GitRepository).OrderBy(x => x.Name).ThenBy(x => x.PathKey).ToListAsync(cancellationToken);

    public async Task<RepositoryLinkResult> AddAsync(int workspaceId, GitRepository candidate, CancellationToken cancellationToken = default)
    {
        // SQLite's serializable write transaction also serializes concurrent path registrations.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!await context.Workspaces.AnyAsync(x => x.Id == workspaceId && x.Active, cancellationToken))
            return new(RepositoryLinkStatus.WorkspaceNotFound);

        var repository = await context.GitRepositories.SingleOrDefaultAsync(x => x.PathKey == candidate.PathKey, cancellationToken);
        if (repository != null && await context.WorkspaceRepositories.AnyAsync(
            x => x.WorkspaceId == workspaceId && x.GitRepositoryId == repository.Id, cancellationToken))
            return new(RepositoryLinkStatus.Duplicate, repository);

        repository ??= candidate;
        context.WorkspaceRepositories.Add(new() { WorkspaceId = workspaceId, GitRepository = repository });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(RepositoryLinkStatus.Added, repository);
    }

    public async Task<bool> RemoveAsync(int workspaceId, int repositoryId, CancellationToken cancellationToken = default) =>
        await context.WorkspaceRepositories.Where(x => x.WorkspaceId == workspaceId
            && x.GitRepositoryId == repositoryId && x.Workspace.Active).ExecuteDeleteAsync(cancellationToken) == 1;
}
