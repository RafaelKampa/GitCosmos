using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;
namespace GitCosmos.Application.Tests;
public class LocalFilesServiceTests
{
    [Theory]
    [InlineData("pt-BR")] [InlineData("es")] [InlineData("en")]
    public async Task ValidatesScopedRequestsAndPreservesTheApplicationPipeline(string language)
    {
        RequestLanguage.Current = language; var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>(); var links = new Mock<IRepositoryLinkRepository>(); var files = new Mock<ILocalFiles>();
        var service = new LocalFilesService(catalog.Object, workspaces.Object, links.Object, files.Object); var request = new LocalFilesRequest(1, ["a.cs"], "working", "info");
        foreach (var invalid in new[] { request with { RepositoryId = 0 }, request with { WorkspaceId = -1 }, request with { Paths = null! }, request with { Paths = [] }, request with { Paths = new string[501] }, request with { Paths = ["a", "a"] }, request with { Paths = [null!] }, request with { Paths = ["\n"] }, request with { Paths = [new string('x', 2001)] }, request with { Area = "bad" }, request with { Action = "shell" }, request with { Token = null! }, request with { Token = "bad" }, request with { Token = new string('Z', 64) }, request with { Tool = null! }, request with { Tool = new string('x', 41) }, request with { Pattern = "glob" }, request with { Start = 0 }, request with { Start = 5, End = 4 }, request with { End = 1000001 } }) await Assert.ThrowsAsync<ValidationAppException>(() => service.ReadAsync(invalid, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(request, default)); await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(request with { WorkspaceId = 2 }, default));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]); await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(request with { WorkspaceId = 2 }, default));
        var repo = new GitRepository { Id = 1, Name = "repo", LocalPath = "stored-root" }; catalog.Setup(x => x.FindAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(repo); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([repo]);
        var view = new LocalFilesView("full", "patch", [], [], [], []); var plan = new LocalFilesPlan(new string('A', 64), "discard", ["a.cs"], "preview");
        files.Setup(x => x.ReadAsync("stored-root", It.IsAny<LocalFilesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(view); files.Setup(x => x.PrepareAsync("stored-root", It.IsAny<LocalFilesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        files.Setup(x => x.ExecuteAsync("stored-root", It.IsAny<LocalFilesRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); files.Setup(x => x.DesktopAsync("stored-root", It.IsAny<LocalFilesRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Assert.Same(view, (await service.ReadAsync(request with { WorkspaceId = 2 }, default)).Result); Assert.Same(plan, (await service.PrepareAsync(request with { Action = "discard" }, default)).Result); Assert.True((await service.ExecuteAsync(request with { Action = "ignore", Token = plan.Token, Pattern = "folder" }, default)).Result); Assert.True((await service.DesktopAsync(request with { Action = "line", Tool = "rider" }, default)).Result);
        foreach (var key in new[] { "local.invalid", "local.failed", "local.completed" }) Assert.NotEqual(key, LocalizedText.Get(key));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ControllerKeepsCustomResponseAndStatus(bool success)
    {
        var service = new Mock<ILocalFilesService>(); var request = new LocalFilesRequest(1, ["a.cs"], "working", "info");
        service.Setup(x => x.ReadAsync(request, default)).ReturnsAsync(new CustomResponse<LocalFilesView> { Success = success }); service.Setup(x => x.PrepareAsync(request, default)).ReturnsAsync(new CustomResponse<LocalFilesPlan> { Success = success }); service.Setup(x => x.ExecuteAsync(request, default)).ReturnsAsync(new CustomResponse<bool> { Success = success }); service.Setup(x => x.DesktopAsync(request, default)).ReturnsAsync(new CustomResponse<bool> { Success = success });
        var controller = new LocalFilesController(service.Object); foreach (var result in new[] { await controller.Read(request, default), await controller.Prepare(request, default), await controller.Execute(request, default), await controller.Desktop(request, default) }) Assert.Equal(success ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result.GetType());
    }
}
