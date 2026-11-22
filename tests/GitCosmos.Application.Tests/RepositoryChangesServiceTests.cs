using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
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

public class RepositoryChangesServiceTests
{
    private static GitChangesSnapshot Snapshot() => new(new("main", false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0), []);

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task OnlyRepositoriesLinkedToTheRequestedActiveWorkspaceCanBeRead(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>();
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var reader = new Mock<IRepositoryChangesReader>(MockBehavior.Strict);
        var service = new RepositoryChangesService(workspaces.Object, links.Object, reader.Object);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(new(0, 2), source.Token));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(new(1, 0), source.Token));
        var missing = await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1, 2), source.Token));
        Assert.Equal(LocalizedText.Get("workspace.not_found"), missing.Message);
        links.VerifyNoOtherCalls(); reader.VerifyNoOtherCalls();
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetDiffAsync(new(1, 2, "file", "working"), source.Token));
        reader.VerifyNoOtherCalls();
        links.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync([new GitRepository { Id = 2, Name = "Repo", LocalPath = "linked path" }]);
        var snapshot = Snapshot();
        reader.Setup(x => x.ReadAsync("linked path", source.Token)).ReturnsAsync(snapshot);
        var response = await service.GetAsync(new(1, 2), source.Token);
        Assert.Equal("Repo", response.Result!.Name); Assert.Equal(2, response.Result.Id); Assert.Equal("linked path", response.Result.LocalPath);
        Assert.Same(snapshot.Status, response.Result.Status); Assert.Same(snapshot.Files, response.Result.Files);
        var diff = new GitFileDiff("file", "working", "text", "+content");
        reader.Setup(x => x.ReadDiffAsync("linked path", "file", "working", source.Token)).ReturnsAsync(diff);
        Assert.Same(diff, (await service.GetDiffAsync(new(1, 2, "file", "working"), source.Token)).Result);
        foreach (var request in new[] { new RepositoryDiffRequest(1, 2, "", "working"), new(1, 2, new string('x', 32768), "working"), new(1, 2, "file", "HEAD") })
        {
            var invalid = await Assert.ThrowsAsync<ValidationAppException>(() => service.GetDiffAsync(request, source.Token));
            Assert.Equal(LocalizedText.Get("changes.invalid_selection"), invalid.Message);
        }
        reader.Setup(x => x.ReadAsync("linked path", source.Token)).ThrowsAsync(new OperationCanceledException(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(new(1, 2), source.Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesResponseEnvelopesAndCancellation(bool success)
    {
        using var source = new CancellationTokenSource();
        var service = new Mock<IRepositoryChangesService>();
        var changes = new CustomResponse<RepositoryChangesResponse> { Success = success };
        var diff = new CustomResponse<GitFileDiff> { Success = success };
        var request = new RepositoryDiffRequest(1, 2, "file", "working");
        service.Setup(x => x.GetAsync(new RepositoryChangesRequest(1, 2), source.Token)).ReturnsAsync(changes);
        service.Setup(x => x.GetDiffAsync(request, source.Token)).ReturnsAsync(diff);
        var controller = new RepositoryChangesController(service.Object);
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.GetChanges(1, 2, source.Token));
        Assert.Same(changes, result.Value); Assert.Equal(success ? 200 : 400, result.StatusCode);
        var patch = Assert.IsAssignableFrom<ObjectResult>(await controller.GetDiff(request, source.Token));
        Assert.Same(diff, patch.Value); Assert.Equal(success ? 200 : 400, patch.StatusCode);
    }

    [Fact]
    public async Task DependencyInjectionResolvesChangesReadersAndServices()
    {
        await using var app = ApiHost.Build([]); using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryChangesService>(scope.ServiceProvider.GetRequiredService<IRepositoryChangesService>());
        Assert.IsType<RepositoryChangesReader>(scope.ServiceProvider.GetRequiredService<IRepositoryChangesReader>());
        Assert.IsType<GitProcessRunner>(scope.ServiceProvider.GetRequiredService<IBoundedGitProcessRunner>());
    }
}
