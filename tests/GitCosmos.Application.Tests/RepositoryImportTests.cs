using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.FileSystem;
using GitCosmos.Infrastructure.Git;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryImportTests
{
    [Theory]
    [InlineData(RepositoryLinkStatus.Added, "added")]
    [InlineData(RepositoryLinkStatus.Duplicate, "duplicate")]
    public async Task SelectedRepositoryTakesPrecedenceWithoutScanningItsNestedRepositories(RepositoryLinkStatus linkStatus, string expectedStatus)
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var nested = Path.Combine(fixture.RepositoryPath, "nested");
        await fixture.RunAsync(fixture.RepositoryPath, "init", "--quiet", "--template=", nested);
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, default)).ReturnsAsync(true);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        links.Setup(x => x.AddAsync(1, It.Is<GitRepository>(r => r.LocalPath == fixture.RepositoryPath), default))
            .ReturnsAsync(new RepositoryLinkResult(linkStatus));
        var reader = new Mock<IChildFolderReader>(MockBehavior.Strict);
        var response = await new RepositoryImportService(workspaces.Object, links.Object,
            new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10))), reader.Object)
            .ImportAsync(new(1, " " + fixture.RepositoryPath + Path.DirectorySeparatorChar + " "));
        var item = Assert.Single(response.Result!.Items);
        Assert.Equal(fixture.RepositoryPath, item.LocalPath);
        Assert.Equal(expectedStatus, item.Status);
        reader.VerifyNoOtherCalls();
        links.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectionFailureOrCancellationDoesNotTriggerChildDiscovery(bool cancelled)
    {
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, default)).ReturnsAsync(true);
        var inspector = new Mock<IGitRepositoryInspector>();
        Exception failure = cancelled ? new OperationCanceledException() : new OperationFailedAppException("Git unavailable");
        inspector.Setup(x => x.InspectAsync("parent", default)).ThrowsAsync(failure);
        var reader = new Mock<IChildFolderReader>(MockBehavior.Strict);
        var service = new RepositoryImportService(workspaces.Object, Mock.Of<IRepositoryLinkRepository>(), inspector.Object, reader.Object);
        var exception = await Record.ExceptionAsync(() => service.ImportAsync(new(1, "parent")));
        Assert.Same(failure, exception);
        reader.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ImportsOnlyImmediateGitRootsAndReportsAllOutcomes(string language)
    {
        RequestLanguage.Current = language;
        await using var fixture = await GitTestDirectory.CreateAsync();
        var parent = Path.Combine(fixture.Root, "ReposTestes");
        Directory.CreateDirectory(parent);
        var first = Path.Combine(parent, "GitCosmosTest1");
        var second = Path.Combine(parent, "GitCosmosTest2");
        var other = Directory.CreateDirectory(Path.Combine(parent, "OutraPasta")).FullName;
        var nested = Path.Combine(other, "RepositorioValido");
        foreach (var path in new[] { first, second, nested })
            await fixture.RunAsync(parent, "init", "--quiet", "--template=", path);
        File.WriteAllText(Path.Combine(parent, "file.txt"), "preserve");
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(true);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        links.Setup(x => x.AddAsync(1, It.Is<GitRepository>(r => r.LocalPath == first), source.Token))
            .ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Added));
        links.Setup(x => x.AddAsync(1, It.Is<GitRepository>(r => r.LocalPath == second), source.Token))
            .ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Duplicate));
        var service = new RepositoryImportService(workspaces.Object, links.Object,
            new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10))), new ChildFolderReader());
        var response = await service.ImportAsync(new(1, parent), source.Token);
        Assert.True(response.Success);
        Assert.Collection(response.Result!.Items,
            item => { Assert.Equal(first, item.LocalPath); Assert.Equal("added", item.Status); },
            item => { Assert.Equal(second, item.LocalPath); Assert.Equal("duplicate", item.Status); },
            item => { Assert.Equal(other, item.LocalPath); Assert.Equal("ignored", item.Status); });
        Assert.Equal(LocalizedText.Get("import.completed", 1, 1, 1, 0), Assert.Single(response.Messages));
        Assert.Equal(LocalizedText.Get("import.added"), response.Result.Items[0].Message);
        Assert.Equal(LocalizedText.Get("import.duplicate"), response.Result.Items[1].Message);
        Assert.DoesNotContain(response.Result.Items, x => x.LocalPath == nested);
        Assert.True(Directory.Exists(nested));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(parent, "file.txt")));
        links.VerifyAll();
    }

    [Fact]
    public async Task IgnoresOrdinaryChildFoldersInsideAnAncestorRepository()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var container = Directory.CreateDirectory(Path.Combine(fixture.RepositoryPath, "container")).FullName;
        var child = Directory.CreateDirectory(Path.Combine(container, "ordinary")).FullName;
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, default)).ReturnsAsync(true);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var response = await new RepositoryImportService(workspaces.Object, links.Object,
            new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10))), new ChildFolderReader())
            .ImportAsync(new(1, container));
        var item = Assert.Single(response.Result!.Items, x => x.LocalPath == child);
        Assert.Equal("ignored", item.Status);
        Assert.Equal(LocalizedText.Get("import.not_root"), item.Message);
        links.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("inspection")]
    [InlineData("persistence")]
    [InlineData("workspace")]
    public async Task ReportsPartialFailureAndContinuesWithOtherChildren(string failure)
    {
        var first = Path.GetFullPath("first"); var second = Path.GetFullPath("second");
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, default)).ReturnsAsync(true);
        var reader = new Mock<IChildFolderReader>();
        reader.Setup(x => x.GetImmediateFolders("parent", default)).Returns([first, second]);
        var inspector = new Mock<IGitRepositoryInspector>();
        inspector.Setup(x => x.InspectAsync("parent", default)).ThrowsAsync(new ValidationAppException("Not a repository"));
        inspector.Setup(x => x.InspectAsync(first, default)).ReturnsAsync(new LocalGitRepository("first", first, first.ToUpperInvariant()));
        inspector.Setup(x => x.InspectAsync(second, default)).ReturnsAsync(new LocalGitRepository("second", second, second.ToUpperInvariant()));
        var links = new Mock<IRepositoryLinkRepository>();
        links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), default)).ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Added));
        if (failure == "inspection") inspector.Setup(x => x.InspectAsync(first, default)).ThrowsAsync(new OperationFailedAppException("Git missing"));
        if (failure == "persistence") links.Setup(x => x.AddAsync(1, It.Is<GitRepository>(r => r.LocalPath == first), default)).ThrowsAsync(new IOException("private diagnostic"));
        if (failure == "workspace") links.Setup(x => x.AddAsync(1, It.Is<GitRepository>(r => r.LocalPath == first), default)).ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.WorkspaceNotFound));
        var result = await new RepositoryImportService(workspaces.Object, links.Object, inspector.Object, reader.Object).ImportAsync(new(1, "parent"));
        Assert.Equal("failed", result.Result!.Items[0].Status);
        Assert.Equal("added", result.Result.Items[1].Status);
        Assert.DoesNotContain("private diagnostic", result.Result.Items[0].Message);
    }

    [Fact]
    public async Task ValidatesBeforeScanningAndHandlesEmptyFolders()
    {
        var workspaces = new Mock<IWorkspaceRepository>();
        var reader = new Mock<IChildFolderReader>(MockBehavior.Strict);
        var inspector = new Mock<IGitRepositoryInspector>(MockBehavior.Strict);
        var service = new RepositoryImportService(workspaces.Object, Mock.Of<IRepositoryLinkRepository>(), inspector.Object, reader.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ImportAsync(new(0, "parent")));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ImportAsync(new(1, " ")));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.ImportAsync(new(1, new string('x', 32768))));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ImportAsync(new(1, "parent")));
        reader.VerifyNoOtherCalls();
        workspaces.Setup(x => x.ExistsAsync(1, default)).ReturnsAsync(true);
        inspector.Setup(x => x.InspectAsync("parent", default)).ThrowsAsync(new ValidationAppException("Not a repository"));
        reader.Setup(x => x.GetImmediateFolders("parent", default)).Returns([]);
        Assert.Empty((await service.ImportAsync(new(1, "parent"))).Result!.Items);
        inspector.Verify(x => x.InspectAsync("parent", default), Times.Once);
        inspector.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PropagatesCancellationWithoutStartingTheNextRepository(bool duringLink)
    {
        using var source = new CancellationTokenSource();
        var first = Path.GetFullPath("first");
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(true);
        var reader = new Mock<IChildFolderReader>();
        reader.Setup(x => x.GetImmediateFolders("parent", source.Token)).Returns([first, Path.GetFullPath("second")]);
        var inspector = new Mock<IGitRepositoryInspector>();
        inspector.Setup(x => x.InspectAsync("parent", source.Token)).ThrowsAsync(new ValidationAppException("Not a repository"));
        inspector.Setup(x => x.InspectAsync(first, source.Token)).ReturnsAsync(new LocalGitRepository("first", first, first));
        var links = new Mock<IRepositoryLinkRepository>();
        if (duringLink) links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        else inspector.Setup(x => x.InspectAsync(first, source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        var service = new RepositoryImportService(workspaces.Object, links.Object, inspector.Object, reader.Object);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ImportAsync(new(1, "parent"), source.Token));
        inspector.Verify(x => x.InspectAsync(first, source.Token), Times.Once);
    }

    [Fact]
    public void FolderReaderRejectsInvalidPathsAndSupportsCancellation()
    {
        var reader = new ChildFolderReader();
        Assert.Throws<ValidationAppException>(() => reader.GetImmediateFolders("relative"));
        Assert.Throws<ValidationAppException>(() => reader.GetImmediateFolders(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
        using var source = new CancellationTokenSource(); source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => reader.GetImmediateFolders("relative", source.Token));
        var root = Directory.CreateTempSubdirectory("GitCosmos-import-empty-");
        try { Assert.Empty(reader.GetImmediateFolders(root.FullName)); }
        finally { Directory.Delete(root.FullName); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesResponseAndCancellation(bool success)
    {
        using var source = new CancellationTokenSource();
        var response = new CustomResponse<RepositoryImportResponse> { Success = success, Result = new([]) };
        var request = new AddWorkspaceRepositoryRequest(1, "parent");
        var service = new Mock<IRepositoryImportService>(MockBehavior.Strict);
        service.Setup(x => x.ImportAsync(request, source.Token)).ReturnsAsync(response);
        var result = Assert.IsAssignableFrom<ObjectResult>(await new RepositoryImportController(service.Object).ImportRepositories(request, source.Token));
        Assert.Same(response, result.Value);
        Assert.Equal(success ? 200 : 400, result.StatusCode);
    }

    [Fact]
    public async Task DependencyInjectionResolvesImportServices()
    {
        await using var app = ApiHost.Build([]);
        using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryImportService>(scope.ServiceProvider.GetRequiredService<IRepositoryImportService>());
        Assert.IsType<ChildFolderReader>(scope.ServiceProvider.GetRequiredService<IChildFolderReader>());
    }
}
