using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Data;
using GitCosmos.Infrastructure.Git;
using GitCosmos.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkspaceStore = GitCosmos.Infrastructure.Repositories.WorkspaceRepository;

namespace GitCosmos.Application.Tests;

public class RepositoryLinkPersistenceTests
{
    [Fact]
    public async Task Links_PersistShareAndRemoveWithoutDeletingFilesOrOtherWorkspaces()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite(connection).Options;
        var inspector = new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10)));
        var sentinel = Path.Combine(fixture.RepositoryPath, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "preserve");
        int firstId, secondId, repositoryId;
        await using (var context = new GitCosmosDbContext(options))
        {
            await context.Database.MigrateAsync();
            var store = new WorkspaceStore(context);
            firstId = await store.CreateAsync(new Workspace { Name = "First", NameKey = "FIRST" });
            secondId = await store.CreateAsync(new Workspace { Name = "Second", NameKey = "SECOND" });
            var hidden = new Workspace { Name = "Hidden", NameKey = "HIDDEN", Active = false };
            await store.CreateAsync(hidden);
            Assert.False(await store.ExistsAsync(hidden.Id));
            var links = new RepositoryLinkRepository(context);
            var candidate = new GitRepository { Name = "candidate", LocalPath = fixture.RepositoryPath, PathKey = fixture.RepositoryPath.ToUpperInvariant() };
            Assert.Equal(RepositoryLinkStatus.WorkspaceNotFound, (await links.AddAsync(hidden.Id, candidate)).Status);
            Assert.Empty(await links.GetAllAsync(hidden.Id));
            Assert.False(await links.RemoveAsync(hidden.Id, 1));
        }
        await using (var context = new GitCosmosDbContext(options))
        {
            var service = Service(context, inspector);
            Assert.Empty((await service.GetAllAsync(firstId)).Result!);
            var result = await service.AddAsync(new(firstId, fixture.RepositoryPath));
            repositoryId = result.Result!.Id;
            Assert.True(repositoryId > 0);
            Assert.Equal(fixture.RepositoryPath, result.Result.LocalPath);
            var nested = Directory.CreateDirectory(Path.Combine(fixture.RepositoryPath, "nested")).FullName;
            await Assert.ThrowsAsync<ConflictAppException>(() => service.AddAsync(new(firstId, nested)));
        }
        await using (var context = new GitCosmosDbContext(options))
        {
            var service = Service(context, inspector);
            Assert.Equal(repositoryId, (await service.AddAsync(new(secondId, fixture.RepositoryPath.ToUpperInvariant()))).Result!.Id);
            Assert.Single(await context.GitRepositories.ToListAsync());
            Assert.Equal(2, await context.WorkspaceRepositories.CountAsync());
            Assert.Single((await service.GetAllAsync(firstId)).Result!);
        }
        await using (var context = new GitCosmosDbContext(options))
        {
            var service = Service(context, inspector);
            await service.RemoveAsync(firstId, repositoryId);
            Assert.Empty((await service.GetAllAsync(firstId)).Result!);
            Assert.Single((await service.GetAllAsync(secondId)).Result!);
            await Assert.ThrowsAsync<NotFoundAppException>(() => service.RemoveAsync(firstId, repositoryId));
            Assert.Equal("preserve", await File.ReadAllTextAsync(sentinel));
        }
        await using (var context = new GitCosmosDbContext(options))
        {
            var service = Service(context, inspector);
            Assert.Equal(repositoryId, (await service.AddAsync(new(firstId, fixture.RepositoryPath))).Result!.Id);
            await new WorkspaceService(new WorkspaceStore(context)).DeleteAsync(firstId);
            Assert.Single((await service.GetAllAsync(secondId)).Result!);
            Assert.Single(await context.GitRepositories.ToListAsync());
            Assert.Equal("preserve", await File.ReadAllTextAsync(sentinel));
        }
        // Removing a link remains possible after its physical directory disappears.
        var moved = Path.Combine(fixture.Root, "moved-repository");
        Directory.Move(fixture.RepositoryPath, moved);
        await using (var context = new GitCosmosDbContext(options))
        {
            await Service(context, inspector).RemoveAsync(secondId, repositoryId);
            Assert.Empty(await context.WorkspaceRepositories.ToListAsync());
            Assert.Single(await context.GitRepositories.ToListAsync());
            Assert.True(File.Exists(Path.Combine(moved, "keep.txt")));
        }
    }

    [Fact]
    public async Task ConcurrentRegistrations_ReuseTheSamePathIdentity()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var database = Path.Combine(fixture.Root, "concurrent.db");
        var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite("Data Source=" + database + ";Pooling=False").Options;
        int first, second;
        await using (var context = new GitCosmosDbContext(options))
        {
            await context.Database.MigrateAsync();
            var store = new WorkspaceStore(context);
            first = await store.CreateAsync(new Workspace { Name = "A", NameKey = "A" });
            second = await store.CreateAsync(new Workspace { Name = "B", NameKey = "B" });
        }
        async Task<RepositoryLinkResult> Link(int workspaceId)
        {
            await using var context = new GitCosmosDbContext(options);
            return await new RepositoryLinkRepository(context).AddAsync(workspaceId,
                new GitRepository { Name = "repo", LocalPath = fixture.RepositoryPath, PathKey = fixture.RepositoryPath.ToUpperInvariant() });
        }
        var results = await Task.WhenAll(Task.Run(() => Link(first)), Task.Run(() => Link(second)));
        Assert.All(results, result => Assert.Equal(RepositoryLinkStatus.Added, result.Status));
        await using (var context = new GitCosmosDbContext(options))
        {
            Assert.Single(await context.GitRepositories.ToListAsync());
            Assert.Equal(2, await context.WorkspaceRepositories.CountAsync());
        }

    }

    private static WorkspaceRepositoryService Service(GitCosmosDbContext context, IGitRepositoryInspector inspector) =>
        new(new WorkspaceStore(context), new RepositoryLinkRepository(context), inspector);
}
