using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.API.Services;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Common;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Repository = GitCosmos.Infrastructure.Repositories.WorkspaceRepository;

namespace GitCosmos.Application.Tests;

public class WorkspaceTests
{
    [Theory]
    [InlineData("pt-BR", "criado")]
    [InlineData("es", "creado")]
    [InlineData("en", "created")]
    public async Task Create_TrimsNormalizesAndLocalizes(string language, string expected)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        repository.Setup(x => x.NameExistsAsync("MY WORKSPACE", source.Token)).ReturnsAsync(false);
        Workspace? saved = null;
        repository.Setup(x => x.CreateAsync(It.IsAny<Workspace>(), source.Token))
            .Callback<Workspace, CancellationToken>((entity, _) => saved = entity).ReturnsAsync(12);
        var response = await new WorkspaceService(repository.Object).CreateAsync(new("  My Workspace  "), source.Token);
        Assert.True(response.Success);
        Assert.Equal(12, response.Result!.Id);
        Assert.Equal("My Workspace", response.Result.Name);
        Assert.Equal(0, response.Result.RepositoryCount);
        Assert.Contains(expected, Assert.Single(response.Messages));
        Assert.Equal("MY WORKSPACE", saved!.NameKey);
        Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
        Assert.Equal(saved.CreatedAt, response.Result.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_RejectsMissingNameBeforePersistence(string? name)
    {
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ValidationAppException>(() => new WorkspaceService(repository.Object).CreateAsync(new(name!)));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Create_RejectsLongNamesAndDuplicatesWithLocalizedErrors(string language)
    {
        RequestLanguage.Current = language;
        var repository = new Mock<IWorkspaceRepository>();
        var service = new WorkspaceService(repository.Object);
        var invalid = await Assert.ThrowsAsync<ValidationAppException>(() => service.CreateAsync(new(new string('x', 101))));
        Assert.Contains(LocalizedText.Get("workspace.name_length"), invalid.Errors);
        repository.Setup(x => x.NameExistsAsync("EXISTING", default)).ReturnsAsync(true);
        var conflict = await Assert.ThrowsAsync<ConflictAppException>(() => service.CreateAsync(new("existing")));
        Assert.Equal(LocalizedText.Get("workspace.name_exists"), conflict.Message);
        repository.Verify(x => x.CreateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Setup(x => x.CreateAsync(It.IsAny<Workspace>(), default)).ReturnsAsync(1);
        Assert.True((await service.CreateAsync(new(new string('x', 100)))).Success);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    [InlineData(int.MaxValue, 1)]
    public async Task List_ValidatesPagination(int page, int size)
    {
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ValidationAppException>(() => new WorkspaceService(repository.Object).GetAllAsync(new(page, size)));
    }

    [Fact]
    public async Task List_ReturnsDefaultEmptyPageAndRequestedPageWithCounts()
    {
        using var source = new CancellationTokenSource();
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetPageAsync(0, 10, source.Token)).ReturnsAsync(new WorkspacePage([], 0));
        var service = new WorkspaceService(repository.Object);
        var empty = await service.GetAllAsync(new(), source.Token);
        Assert.True(empty.Success);
        Assert.Empty(empty.Result!.Items);
        Assert.Equal(0, empty.TotalRows);
        Assert.Equal(0, empty.Result.TotalPages);
        var workspace = new Workspace { Id = 4, Name = "Team", Repositories = [new()] };
        repository.Setup(x => x.GetPageAsync(1, 2, source.Token)).ReturnsAsync(new WorkspacePage([workspace], 3));
        var response = await service.GetAllAsync(new(1, 2), source.Token);
        Assert.Equal(1, response.Result!.Page);
        Assert.Equal(2, response.Result.PageSize);
        Assert.Equal(2, response.Result.TotalPages);
        Assert.Equal(3, response.TotalRows);
        Assert.Equal(1, Assert.Single(response.Result.Items).RepositoryCount);
    }

    [Fact]
    public async Task Controllers_PreserveBusinessEnvelopeAndFailureStatus()
    {
        using var source = new CancellationTokenSource();
        var service = new Mock<IWorkspaceService>(MockBehavior.Strict);
        var controller = new WorkspaceController(service.Object);
        var request = new CreateWorkspaceRequest("Team");
        var created = CustomResponse<WorkspaceResponse>.Ok(new(1, "Team", DateTime.UtcNow, 0));
        service.Setup(x => x.CreateAsync(request, source.Token)).ReturnsAsync(created);
        Assert.Same(created, Assert.IsType<OkObjectResult>(await controller.CreateWorkspace(request, source.Token)).Value);
        service.Setup(x => x.CreateAsync(request, source.Token)).ReturnsAsync(CustomResponse<WorkspaceResponse>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.CreateWorkspace(request, source.Token));
        var pagination = new PaginationRequest();
        var listed = CustomResponse<PagedResult<WorkspaceResponse>>.Ok(new([], 0, 0, 10));
        service.Setup(x => x.GetAllAsync(pagination, source.Token)).ReturnsAsync(listed);
        Assert.Same(listed, Assert.IsType<OkObjectResult>(await controller.GetAllWorkspaces(pagination, source.Token)).Value);
        service.Setup(x => x.GetAllAsync(pagination, source.Token)).ReturnsAsync(CustomResponse<PagedResult<WorkspaceResponse>>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.GetAllWorkspaces(pagination, source.Token));
    }

    [Fact]
    public void EntityLifecycle_UsesUtcAndSuppliedTimestamps()
    {
        var workspace = new Workspace { TotalRows = 3 };
        Assert.True(workspace.Active);
        Assert.Equal(3, workspace.TotalRows);
        var changed = DateTime.UtcNow.AddDays(-1);
        workspace.Deactivate(changed);
        Assert.False(workspace.Active);
        Assert.Equal(changed, workspace.DeletedAt);
        Assert.Equal(changed, workspace.UpdatedAt);
        workspace.Activate(changed);
        Assert.True(workspace.Active);
        Assert.Null(workspace.DeletedAt);
        Assert.Equal(changed, workspace.UpdatedAt);
        workspace.Deactivate();
        Assert.Equal(DateTimeKind.Utc, workspace.DeletedAt!.Value.Kind);
        workspace.Activate();
        workspace.RefreshUpdatedAt();
        Assert.Equal(DateTimeKind.Utc, workspace.UpdatedAt!.Value.Kind);
    }

    [Fact]
    public async Task Sqlite_MigrationsPersistSharedRepositoriesEnforceConstraintsAndPaginate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection().AddDbContext<GitCosmosDbContext>(x => x.UseSqlite(connection));
        await using var provider = services.BuildServiceProvider();
        var startup = new DatabaseMigrationService(provider.GetRequiredService<IServiceScopeFactory>());
        await startup.StartAsync(default);
        await startup.StartAsync(default); // Existing databases are upgraded, never reset.
        await startup.StopAsync(default);
        var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite(connection).Options;
        int firstId;
        await using (var context = new GitCosmosDbContext(options))
        {
            var repository = new Repository(context);
            firstId = await repository.CreateAsync(new Workspace { Name = "Bravo", NameKey = "BRAVO" });
            await repository.CreateAsync(new Workspace { Name = "Alpha", NameKey = "ALPHA" });
            await repository.CreateAsync(new Workspace { Name = "Hidden", NameKey = "HIDDEN", Active = false });
            Assert.True(await repository.NameExistsAsync("BRAVO"));
            Assert.False(await repository.NameExistsAsync("HIDDEN"));
            var git = new GitRepository { Name = "repo", LocalPath = @"C:\repos\repo", PathKey = @"C:\REPOS\REPO" };
            var workspaces = await context.Workspaces.Where(x => x.Active).ToListAsync();
            foreach (var workspace in workspaces) workspace.Repositories.Add(new() { GitRepository = git });
            await context.SaveChangesAsync();
            var page = await repository.GetPageAsync(0, 1);
            Assert.Equal(2, page.TotalRows);
            Assert.Equal("Alpha", Assert.Single(page.Items).Name);
            Assert.Single(page.Items[0].Repositories);
            Assert.Equal(firstId, Assert.Single((await repository.GetPageAsync(1, 1)).Items).Id);
            Assert.Empty((await repository.GetPageAsync(2, 1)).Items);
            context.Workspaces.Add(new() { Name = "bravo", NameKey = "BRAVO" });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.GitRepositories.Add(new() { Name = "duplicate", LocalPath = "other", PathKey = git.PathKey });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            context.ChangeTracker.Clear();
            context.WorkspaceRepositories.Add(new() { WorkspaceId = firstId, GitRepositoryId = git.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        await using (var reopened = new GitCosmosDbContext(options))
        {
            Assert.Equal(3, await reopened.Workspaces.CountAsync());
            var git = await reopened.GitRepositories.Include(x => x.Workspaces).SingleAsync();
            Assert.Equal(2, git.Workspaces.Count);
            Assert.Equal(@"C:\repos\repo", git.LocalPath);
            Assert.All(git.Workspaces, link => Assert.True(link.AddedAt > DateTime.MinValue));
            reopened.Workspaces.Remove(await reopened.Workspaces.SingleAsync(x => x.Id == firstId));
            await reopened.SaveChangesAsync();
            Assert.Single(await reopened.WorkspaceRepositories.ToListAsync());
            Assert.Single(await reopened.GitRepositories.ToListAsync()); // Workspace deletion never deletes Git data.
            var assembly = reopened.GetService<IMigrationsAssembly>();
            Assert.NotNull(assembly.ModelSnapshot!.Model);
            foreach (var type in assembly.Migrations.Values)
                Assert.NotNull(assembly.CreateMigration(type, reopened.Database.ProviderName!).TargetModel);
            await reopened.GetService<IMigrator>().MigrateAsync("0");
            await reopened.Database.MigrateAsync();
            Assert.Empty(await reopened.Workspaces.ToListAsync());
        }
    }

    [Fact]
    public async Task CompositionRoot_ResolvesWorkspaceAndMigrationServices()
    {
        await using var app = ApiTestHost.Build();
        using var scope = app.Services.CreateScope();
        Assert.IsType<WorkspaceService>(scope.ServiceProvider.GetRequiredService<IWorkspaceService>());
        Assert.IsType<Repository>(scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>());
        Assert.Contains(app.Services.GetServices<IHostedService>(), x => x is DatabaseMigrationService);
    }
}
