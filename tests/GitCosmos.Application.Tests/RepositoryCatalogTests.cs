using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Data;
using GitCosmos.Infrastructure.Git;
using GitCosmos.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryCatalogTests
{
    private static GitWorkingTreeStatus Status() => new("main", false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static RepositoryCatalogService Service(Mock<IRepositoryCatalogRepository> catalog, IRepositoryStatusReader? status = null, IRepositoryChangesReader? changes = null,
        IGitRepositoryInspector? inspector = null, IChildFolderReader? folders = null, IRepositoryCloner? cloner = null) =>
        new(catalog.Object, status ?? Mock.Of<IRepositoryStatusReader>(), changes ?? Mock.Of<IRepositoryChangesReader>(), inspector ?? Mock.Of<IGitRepositoryInspector>(), folders ?? Mock.Of<IChildFolderReader>(), cloner ?? Mock.Of<IRepositoryCloner>());

    [Fact]
    public async Task RegistryPersistsStandaloneRepositoriesWithoutDuplicatingSharedPaths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<GitCosmosDbContext>().UseSqlite(connection).Options;
        int id;
        await using (var context = new GitCosmosDbContext(options))
        {
            await context.Database.MigrateAsync();
            var registry = new RepositoryCatalogRepository(context);
            var saved = await registry.RegisterAsync(new GitRepository { Name = "Repo", LocalPath = "C:/Repo", PathKey = "C:/REPO" });
            id = saved.Repository.Id; Assert.True(saved.Created);
            var duplicate = await registry.RegisterAsync(new GitRepository { Name = "Other", LocalPath = "c:/repo", PathKey = "C:/REPO" });
            Assert.False(duplicate.Created); Assert.Equal(id, duplicate.Repository.Id);
            Assert.Empty(context.Workspaces);
            var active = new Workspace { Name = "Team", NameKey = "TEAM" }; var inactive = new Workspace { Name = "Old", NameKey = "OLD", Active = false };
            context.Workspaces.AddRange(active, inactive); await context.SaveChangesAsync();
            context.WorkspaceRepositories.AddRange(new GitCosmos.Domain.Entities.WorkspaceRepository { WorkspaceId = active.Id, GitRepositoryId = id }, new GitCosmos.Domain.Entities.WorkspaceRepository { WorkspaceId = inactive.Id, GitRepositoryId = id });
            await context.SaveChangesAsync();
        }
        await using (var context = new GitCosmosDbContext(options))
        {
            var registry = new RepositoryCatalogRepository(context);
            var snapshot = await registry.GetAsync(); Assert.Single(snapshot.Workspaces);
            var repo = Assert.Single(snapshot.Repositories); Assert.Single(repo.Workspaces); Assert.Equal(id, repo.Id);
            Assert.Equal("Repo", (await registry.FindAsync(id))!.Name); Assert.Null(await registry.FindAsync(-1));
            context.Workspaces.Single(x => x.Name == "Team").Active = false; await context.SaveChangesAsync();
            Assert.Empty((await registry.GetAsync()).Repositories.Single().Workspaces);
        }
    }

    [Fact]
    public async Task OverviewDeduplicatesAndPreservesUnavailableAndUnknownStates()
    {
        var catalog = new Mock<IRepositoryCatalogRepository>();
        var repositories = Enumerable.Range(1, 4).Select(i => new GitRepository { Id = i, Name = "Repo" + i, LocalPath = "path" + i }).ToList();
        repositories[0].Workspaces.Add(new GitCosmos.Domain.Entities.WorkspaceRepository { WorkspaceId = 1 });
        catalog.Setup(x => x.GetAsync(default)).ReturnsAsync(new RepositoryCatalog([new Workspace { Id = 1, Name = "Team" }], repositories));
        var reader = new Mock<IRepositoryStatusReader>(MockBehavior.Strict);
        reader.Setup(x => x.ReadAsync("path1", default)).ReturnsAsync(Status());
        reader.Setup(x => x.ReadAsync("path2", default)).ReturnsAsync(Status() with { ChangedFiles = 1 });
        reader.Setup(x => x.ReadAsync("path3", default)).ReturnsAsync(Status() with { ChangedFiles = 1, Conflicted = 1 });
        reader.Setup(x => x.ReadAsync("path4", default)).ThrowsAsync(new ValidationAppException("Missing"));
        var service = Service(catalog, reader.Object);
        var index = (await service.GetAsync(false)).Result!;
        Assert.All(index.Repositories, repo => Assert.Equal("unknown", repo.State)); reader.VerifyNoOtherCalls();
        var overview = (await service.GetAsync(true)).Result!;
        Assert.Equal(new[] { "clean", "modified", "conflicted", "unavailable" }, overview.Repositories.Select(x => x.State));
        Assert.Equal("Team", overview.Workspaces.Single().Name); Assert.Equal(new[] { 1 }, overview.Repositories[0].WorkspaceIds);
        Assert.Equal("Missing", overview.Repositories[3].Error);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task StandaloneAdditionAndDiffsDoNotRequireAWorkspace(string language)
    {
        RequestLanguage.Current = language;
        var path = Path.GetFullPath("repo"); var catalog = new Mock<IRepositoryCatalogRepository>();
        var inspector = new Mock<IGitRepositoryInspector>(); var folders = new Mock<IChildFolderReader>(MockBehavior.Strict);
        inspector.Setup(x => x.InspectAsync(path, default)).ReturnsAsync(new LocalGitRepository("Repo", path, path));
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ReturnsAsync(new RegisteredRepository(new GitRepository { Id = 5, Name = "Repo", LocalPath = path }, true));
        var changes = new Mock<IRepositoryChangesReader>();
        changes.Setup(x => x.ReadAsync(path, default)).ReturnsAsync(new GitChangesSnapshot(Status(), []));
        changes.Setup(x => x.ReadDiffAsync(path, "file", "working", default)).ReturnsAsync(new GitFileDiff("file", "working", "text", "+new"));
        var service = Service(catalog, changes: changes.Object, inspector: inspector.Object, folders: folders.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAsync(new("")));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAsync(new(new string('x', 32768))));
        Assert.Equal("added", (await service.AddAsync(new(path))).Result!.Items.Single().Status); folders.VerifyNoOtherCalls();
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ReturnsAsync(new RegisteredRepository(new GitRepository { Id = 5 }, false));
        Assert.Equal(LocalizedText.Get("catalog.already_registered"), (await service.AddAsync(new(path))).Result!.Items.Single().Message);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetChangesAsync(0));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetChangesAsync(5));
        catalog.Setup(x => x.FindAsync(5, default)).ReturnsAsync(new GitRepository { Id = 5, Name = "Repo", LocalPath = path });
        Assert.Equal("Repo", (await service.GetChangesAsync(5)).Result!.Name);
        Assert.Equal("+new", (await service.GetDiffAsync(new(5, "file", "working"))).Result!.Patch);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetDiffAsync(new(5, "", "working")));
    }

    [Fact]
    public async Task ContainerAdditionIgnoresNestedAndAncestorRepositoriesAndReportsPartialFailure()
    {
        var parent = Path.GetFullPath("container"); var first = Path.Combine(parent, "first"); var invalid = Path.Combine(parent, "invalid"); var inherited = Path.Combine(parent, "inherited"); var failed = Path.Combine(parent, "failed");
        var inspector = new Mock<IGitRepositoryInspector>();
        inspector.Setup(x => x.InspectAsync(parent, default)).ThrowsAsync(new ValidationAppException("Not Git"));
        inspector.Setup(x => x.InspectAsync(first, default)).ReturnsAsync(new LocalGitRepository("first", first, first));
        inspector.Setup(x => x.InspectAsync(invalid, default)).ThrowsAsync(new ValidationAppException("Not Git"));
        inspector.Setup(x => x.InspectAsync(inherited, default)).ReturnsAsync(new LocalGitRepository("ancestor", parent, parent));
        inspector.Setup(x => x.InspectAsync(failed, default)).ThrowsAsync(new OperationFailedAppException("Git unavailable"));
        var folders = new Mock<IChildFolderReader>(); folders.Setup(x => x.GetImmediateFolders(parent, default)).Returns([first, invalid, inherited, failed]);
        var catalog = new Mock<IRepositoryCatalogRepository>(); catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ReturnsAsync(new RegisteredRepository(new GitRepository { Id = 1 }, true));
        var service = Service(catalog, inspector: inspector.Object, folders: folders.Object);
        Assert.Equal(new[] { "added", "ignored", "ignored", "failed" }, (await service.AddAsync(new(parent))).Result!.Items.Select(x => x.Status));
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ThrowsAsync(new IOException("private"));
        Assert.Equal("failed", (await service.AddAsync(new(parent))).Result!.Items.First().Status);
    }

    [Fact]
    public async Task StandaloneCloneRequiresReviewedDestinationAndPreservesCompletedCloneOnRegistrationFailure()
    {
        var catalog = new Mock<IRepositoryCatalogRepository>(); var cloner = new Mock<IRepositoryCloner>();
        var plan = new RepositoryClonePlan("https://example.com/repo.git", "parent", "repo", "destination");
        cloner.Setup(x => x.Plan(plan.RemoteUrl, plan.ParentPath)).Returns(plan);
        cloner.Setup(x => x.CloneAsync(plan, default)).ReturnsAsync(new LocalGitRepository("repo", plan.DestinationPath, "KEY"));
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ReturnsAsync(new RegisteredRepository(new GitRepository { Id = 2, Name = "repo", LocalPath = plan.DestinationPath }, true));
        var service = Service(catalog, cloner: cloner.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.PreviewAsync(new("", "parent")));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.PreviewAsync(new(plan.RemoteUrl, "")));
        Assert.Same(plan, (await service.PreviewAsync(new(plan.RemoteUrl, plan.ParentPath))).Result);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CloneAsync(new(plan.RemoteUrl, plan.ParentPath, "wrong")));
        Assert.Equal(2, (await service.CloneAsync(new(plan.RemoteUrl, plan.ParentPath, plan.DestinationPath))).Result!.Id);
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ThrowsAsync(new IOException("private"));
        var failure = await Assert.ThrowsAsync<OperationFailedAppException>(() => service.CloneAsync(new(plan.RemoteUrl, plan.ParentPath, plan.DestinationPath)));
        Assert.Contains(plan.DestinationPath, failure.Message); Assert.DoesNotContain("private", failure.Message);
        catalog.Setup(x => x.RegisterAsync(It.IsAny<GitRepository>(), default)).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CloneAsync(new(plan.RemoteUrl, plan.ParentPath, plan.DestinationPath)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllersDelegateAndKeepResponseEnvelopes(bool success)
    {
        using var source = new CancellationTokenSource(); var service = new Mock<IRepositoryCatalogService>();
        var snapshot = new CustomResponse<RepositoryCatalogResponse> { Success = success };
        var added = new CustomResponse<RepositoryImportResponse> { Success = success };
        var change = new CustomResponse<RepositoryChangesResponse> { Success = success };
        var diff = new CustomResponse<GitFileDiff> { Success = success }; var preview = new CustomResponse<RepositoryClonePlan> { Success = success }; var cloned = new CustomResponse<WorkspaceRepositoryResponse> { Success = success };
        service.Setup(x => x.GetAsync(It.IsAny<bool>(), source.Token)).ReturnsAsync(snapshot);
        service.Setup(x => x.AddAsync(It.IsAny<CatalogAddRequest>(), source.Token)).ReturnsAsync(added);
        service.Setup(x => x.GetChangesAsync(2, source.Token)).ReturnsAsync(change);
        service.Setup(x => x.GetDiffAsync(It.IsAny<CatalogDiffRequest>(), source.Token)).ReturnsAsync(diff);
        service.Setup(x => x.PreviewAsync(It.IsAny<CatalogCloneRequest>(), source.Token)).ReturnsAsync(preview);
        service.Setup(x => x.CloneAsync(It.IsAny<CatalogCloneRequest>(), source.Token)).ReturnsAsync(cloned);
        var controller = new RepositoryCatalogController(service.Object);
        foreach (var result in new[] { await controller.GetExplorer(source.Token), await controller.GetOverview(source.Token), await controller.AddRepository(new("path"), source.Token), await controller.GetChanges(2, source.Token), await controller.GetDiff(new(2,"file","working"),source.Token), await controller.Preview(new("url","parent"),source.Token), await controller.CloneRepository(new("url","parent","destination"),source.Token) })
            Assert.Equal(success ? 200 : 400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        await using var app = ApiTestHost.Build(); using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryCatalogService>(scope.ServiceProvider.GetRequiredService<IRepositoryCatalogService>());
    }

    [Theory]
    [InlineData("+2 -3", 2, 3)]
    [InlineData("+0 -0", 0, 0)]
    public void TrackingCountsComeFromLocalGitHeaders(string counts, int ahead, int behind)
    {
        var status = GitStatusParser.Parse("# branch.oid abc\0# branch.head main\0# branch.upstream origin/main\0# branch.ab " + counts + "\0");
        Assert.Equal(ahead, status.Ahead); Assert.Equal(behind, status.Behind); Assert.Equal("origin/main", status.Upstream);
    }
    [Theory]
    [InlineData("+1")][InlineData("1 -1")][InlineData("+1 1")][InlineData("+x -1")][InlineData("+-1 -1")][InlineData("+1 -x")][InlineData("+1 --1")]
    public void InvalidTrackingHeadersFailInsteadOfInventingCounts(string counts) => Assert.Throws<OperationFailedAppException>(() => GitStatusParser.Parse("# branch.oid abc\0# branch.head main\0# branch.ab " + counts + "\0"));
}
