using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Git;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class WorkspaceDashboardTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task DashboardIsolatesUnavailableRepositoriesAndPreservesOrdering(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.GetFullByIdAsync(1, source.Token)).ReturnsAsync(new Workspace { Id = 1, Name = "Team" });
        var links = new Mock<IRepositoryLinkRepository>();
        links.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync(Enumerable.Range(1, 5)
            .Select(i => new GitRepository { Id = i, Name = "repo" + i, LocalPath = "path" + i }).ToList());
        var reader = new Mock<IRepositoryStatusReader>(MockBehavior.Strict);
        var clean = new GitWorkingTreeStatus("main", false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        reader.Setup(x => x.ReadAsync("path1", source.Token)).ReturnsAsync(clean);
        reader.Setup(x => x.ReadAsync("path2", source.Token)).ThrowsAsync(new ValidationAppException(LocalizedText.Get("repository.invalid_path")));
        reader.Setup(x => x.ReadAsync("path3", source.Token)).ReturnsAsync(clean with { ChangedFiles = 2, Modified = 2 });
        reader.Setup(x => x.ReadAsync("path4", source.Token)).ReturnsAsync(clean with { ChangedFiles = 1, Conflicted = 1 });
        reader.Setup(x => x.ReadAsync("path5", source.Token)).ThrowsAsync(new OperationFailedAppException(LocalizedText.Get("repository.git_timeout")));
        var before = DateTimeOffset.UtcNow;
        var result = await new WorkspaceDashboardService(workspaces.Object, links.Object, reader.Object).GetAsync(1, source.Token);
        Assert.True(result.Success); Assert.Equal("Team", result.Result!.Name); Assert.Equal(1, result.Result.WorkspaceId);
        Assert.InRange(result.Result.CheckedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(new[] { "clean", "unavailable", "modified", "conflicted", "unavailable" }, result.Result.Repositories.Select(x => x.State));
        Assert.Equal("repo2", result.Result.Repositories[1].Name); Assert.Null(result.Result.Repositories[1].Status);
        Assert.Equal(LocalizedText.Get("repository.invalid_path"), result.Result.Repositories[1].Error);
        Assert.Same(clean, result.Result.Repositories[0].Status); Assert.Null(result.Result.Repositories[0].Error);
        reader.VerifyAll();
    }

    [Fact]
    public async Task ValidatesWorkspaceBeforeListingAndSupportsEmptyWorkspaces()
    {
        var workspaces = new Mock<IWorkspaceRepository>();
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var reader = new Mock<IRepositoryStatusReader>(MockBehavior.Strict);
        var service = new WorkspaceDashboardService(workspaces.Object, links.Object, reader.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(0));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(1));
        links.VerifyNoOtherCalls();
        workspaces.Setup(x => x.GetFullByIdAsync(1, default)).ReturnsAsync(new Workspace { Id = 1, Name = "Empty" });
        links.Setup(x => x.GetAllAsync(1, default)).ReturnsAsync([]);
        Assert.Empty((await service.GetAsync(1)).Result!.Repositories);
        reader.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationStopsBeforeTheNextRepository()
    {
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>();
        workspaces.Setup(x => x.GetFullByIdAsync(1, source.Token)).ReturnsAsync(new Workspace { Id = 1 });
        var links = new Mock<IRepositoryLinkRepository>();
        links.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync([new GitRepository { LocalPath = "first" }, new GitRepository { LocalPath = "second" }]);
        var reader = new Mock<IRepositoryStatusReader>();
        reader.Setup(x => x.ReadAsync("first", source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WorkspaceDashboardService(workspaces.Object, links.Object, reader.Object).GetAsync(1, source.Token));
        reader.Verify(x => x.ReadAsync("second", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesEnvelopeAndCancellation(bool success)
    {
        using var source = new CancellationTokenSource();
        var response = new CustomResponse<WorkspaceDashboardResponse> { Success = success };
        var service = new Mock<IWorkspaceDashboardService>();
        service.Setup(x => x.GetAsync(1, source.Token)).ReturnsAsync(response);
        var result = Assert.IsAssignableFrom<ObjectResult>(await new WorkspaceDashboardController(service.Object).GetDashboard(1, source.Token));
        Assert.Equal(success ? 200 : 400, result.StatusCode); Assert.Same(response, result.Value);
    }

    [Fact]
    public async Task DependencyInjectionResolvesDashboardServices()
    {
        await using var app = ApiTestHost.Build(); using var scope = app.Services.CreateScope();
        Assert.IsType<WorkspaceDashboardService>(scope.ServiceProvider.GetRequiredService<IWorkspaceDashboardService>());
        Assert.IsType<RepositoryStatusReader>(scope.ServiceProvider.GetRequiredService<IRepositoryStatusReader>());
    }
}
