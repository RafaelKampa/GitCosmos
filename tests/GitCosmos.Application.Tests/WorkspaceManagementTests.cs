using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Repository = GitCosmos.Infrastructure.Repositories.WorkspaceRepository;

namespace GitCosmos.Application.Tests;

public class WorkspaceManagementTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Rename_NormalizesNameExcludesOwnIdAndLocalizes(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var workspace = new Workspace { Id = 8, Name = "Team", NameKey = "TEAM", Repositories = [new()] };
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetFullByIdAsync(8, source.Token)).ReturnsAsync(workspace);
        repository.Setup(x => x.NameExistsAsync("TEAM", 8, source.Token)).ReturnsAsync(false);
        repository.Setup(x => x.UpdateAsync(workspace, source.Token)).ReturnsAsync(8);
        var response = await new WorkspaceService(repository.Object).UpdateAsync(new(8, "  TEAM  "), source.Token);
        Assert.True(response.Success);
        Assert.Equal("TEAM", response.Result!.Name);
        Assert.Equal(1, response.Result.RepositoryCount);
        Assert.Equal("TEAM", workspace.NameKey);
        Assert.Equal(DateTimeKind.Utc, workspace.UpdatedAt!.Value.Kind);
        Assert.Equal(LocalizedText.Get("workspace.updated"), Assert.Single(response.Messages));
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(0, "Valid")]
    [InlineData(-1, "Valid")]
    [InlineData(1, null)]
    [InlineData(1, "")]
    [InlineData(1, "   ")]
    public async Task Rename_ValidatesBeforePersistence(int id, string? name)
    {
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var service = new WorkspaceService(repository.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateAsync(new(id, name!)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.UpdateAsync(new(1, new string('x', 101))));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Rename_RejectsMissingWorkspaceAndAnotherWorkspacesName(string language)
    {
        RequestLanguage.Current = language;
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetFullByIdAsync(1, default)).ReturnsAsync((Workspace?)null);
        var service = new WorkspaceService(repository.Object);
        var missing = await Assert.ThrowsAsync<NotFoundAppException>(() => service.UpdateAsync(new(1, "Team")));
        Assert.Equal(LocalizedText.Get("workspace.not_found"), missing.Message);
        var workspace = new Workspace { Id = 1, Name = "Before" };
        repository.Setup(x => x.GetFullByIdAsync(1, default)).ReturnsAsync(workspace);
        repository.Setup(x => x.NameExistsAsync("TEAM", 1, default)).ReturnsAsync(true);
        var conflict = await Assert.ThrowsAsync<ConflictAppException>(() => service.UpdateAsync(new(1, "Team")));
        Assert.Equal(LocalizedText.Get("workspace.name_exists"), conflict.Message);
        Assert.Equal("Before", workspace.Name);
        repository.Verify(x => x.UpdateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Delete_ValidatesFindsAndDeactivatesBeforeSaving(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var service = new WorkspaceService(repository.Object);
        foreach (var invalid in new[] { 0, -1 })
        {
            var exception = await Assert.ThrowsAsync<ValidationAppException>(() => service.DeleteAsync(invalid, source.Token));
            Assert.Contains(LocalizedText.Get("workspace.invalid_id"), exception.Errors);
        }
        repository.Setup(x => x.GetFullByIdAsync(4, source.Token)).ReturnsAsync((Workspace?)null);
        var missing = await Assert.ThrowsAsync<NotFoundAppException>(() => service.DeleteAsync(4, source.Token));
        Assert.Equal(LocalizedText.Get("workspace.not_found"), missing.Message);
        var workspace = new Workspace { Id = 4, Name = "Team" };
        repository.Setup(x => x.GetFullByIdAsync(4, source.Token)).ReturnsAsync(workspace);
        repository.Setup(x => x.DeleteAsync(workspace, source.Token)).ReturnsAsync(4);
        var response = await service.DeleteAsync(4, source.Token);
        Assert.True(response.Success);
        Assert.Equal(4, response.Result);
        Assert.False(workspace.Active);
        Assert.Equal(workspace.DeletedAt, workspace.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, workspace.DeletedAt!.Value.Kind);
        Assert.Equal(LocalizedText.Get("workspace.deleted"), Assert.Single(response.Messages));
        repository.VerifyAll();
    }

    [Fact]
    public async Task Controllers_ForwardRequestsTokensAndBusinessEnvelopes()
    {
        using var source = new CancellationTokenSource();
        var service = new Mock<IWorkspaceService>(MockBehavior.Strict);
        var controller = new WorkspaceController(service.Object);
        var request = new UpdateWorkspaceRequest(1, "New");
        var updated = CustomResponse<WorkspaceResponse>.Ok(new(1, "New", DateTime.UtcNow, 0));
        service.Setup(x => x.UpdateAsync(request, source.Token)).ReturnsAsync(updated);
        Assert.Same(updated, Assert.IsType<OkObjectResult>(await controller.UpdateWorkspace(request, source.Token)).Value);
        var failedUpdate = CustomResponse<WorkspaceResponse>.Fail("invalid");
        service.Setup(x => x.UpdateAsync(request, source.Token)).ReturnsAsync(failedUpdate);
        Assert.Same(failedUpdate, Assert.IsType<BadRequestObjectResult>(await controller.UpdateWorkspace(request, source.Token)).Value);
        var deleted = CustomResponse<int?>.Ok(1);
        service.Setup(x => x.DeleteAsync(1, source.Token)).ReturnsAsync(deleted);
        Assert.Same(deleted, Assert.IsType<OkObjectResult>(await controller.DeleteWorkspace(1, source.Token)).Value);
        var failedDelete = CustomResponse<int?>.Fail("invalid");
        service.Setup(x => x.DeleteAsync(1, source.Token)).ReturnsAsync(failedDelete);
        Assert.Same(failedDelete, Assert.IsType<BadRequestObjectResult>(await controller.DeleteWorkspace(1, source.Token)).Value);
    }

    [Fact]
    public async Task Sqlite_RenameAndSoftDeletePersistWithoutRemovingSharedRepositoriesOrFiles()
    {
        var directory = Directory.CreateTempSubdirectory("GitCosmos-workspace-");
        var sentinel = Path.Combine(directory.FullName, "keep.txt");
        await File.WriteAllTextAsync(sentinel, "preserve");
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite(connection).Options;
            int removedId;
            await using (var context = new GitCosmosDbContext(options))
            {
                await context.Database.MigrateAsync();
                var repository = new Repository(context);
                var service = new WorkspaceService(repository);
                var first = (await service.CreateAsync(new("First"))).Result!;
                var second = (await service.CreateAsync(new("Second"))).Result!;
                removedId = first.Id;
                var git = new GitRepository { Name = "shared", LocalPath = directory.FullName, PathKey = directory.FullName.ToUpperInvariant() };
                context.WorkspaceRepositories.AddRange(
                    new() { WorkspaceId = first.Id, GitRepository = git },
                    new() { WorkspaceId = second.Id, GitRepository = git });
                await context.SaveChangesAsync();
                context.ChangeTracker.Clear();
                Assert.False(await repository.NameExistsAsync("FIRST", first.Id));
                Assert.True(await repository.NameExistsAsync("FIRST", second.Id));
                Assert.Null(await repository.GetFullByIdAsync(int.MaxValue));
                var result = await service.UpdateAsync(new(first.Id, new string('x', 100)));
                Assert.Equal(1, result.Result!.RepositoryCount);
                Assert.Equal(100, result.Result.Name.Length);
                await Assert.ThrowsAsync<ConflictAppException>(() => service.UpdateAsync(new(first.Id, "SECOND")));
                await service.DeleteAsync(first.Id);
                Assert.Null(await repository.GetFullByIdAsync(first.Id));
                await Assert.ThrowsAsync<NotFoundAppException>(() => service.UpdateAsync(new(first.Id, "Gone")));
                await Assert.ThrowsAsync<NotFoundAppException>(() => service.DeleteAsync(first.Id));
                Assert.False(await repository.NameExistsAsync(new string('X', 100)));
                Assert.Equal(1, (await repository.GetPageAsync(0, 10)).TotalRows);
                Assert.True((await service.CreateAsync(new(new string('x', 100)))).Success);
            }
            await using (var reopened = new GitCosmosDbContext(options))
            {
                var deleted = await reopened.Workspaces.SingleAsync(x => x.Id == removedId);
                Assert.False(deleted.Active);
                Assert.NotNull(deleted.DeletedAt);
                Assert.NotNull(deleted.UpdatedAt);
                Assert.Empty(await reopened.WorkspaceRepositories.Where(x => x.WorkspaceId == removedId).ToListAsync());
                Assert.Single(await reopened.GitRepositories.ToListAsync());
                Assert.Single(await reopened.WorkspaceRepositories.ToListAsync());
                Assert.Equal("preserve", await File.ReadAllTextAsync(sentinel));
            }
        }
        finally
        {
            File.Delete(sentinel);
            Directory.Delete(directory.FullName);
        }
    }

    [Fact]
    public async Task Sqlite_FailedLinkRemovalRollsBackTheWorkspaceDeletion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite(connection).Options;
        await using var context = new GitCosmosDbContext(options);
        await context.Database.MigrateAsync();
        var workspace = new Workspace { Name = "Keep", NameKey = "KEEP", Repositories = [new() { GitRepository = new() { Name = "repo", LocalPath = "local", PathKey = "LOCAL" } }] };
        context.Workspaces.Add(workspace);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE TRIGGER block_link_removal BEFORE DELETE ON workspace_repositories BEGIN SELECT RAISE(ABORT, 'blocked'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => new WorkspaceService(new Repository(context)).DeleteAsync(workspace.Id));
        await using var reopened = new GitCosmosDbContext(options);
        Assert.True((await reopened.Workspaces.SingleAsync()).Active);
        Assert.Single(await reopened.WorkspaceRepositories.ToListAsync());
    }
}
