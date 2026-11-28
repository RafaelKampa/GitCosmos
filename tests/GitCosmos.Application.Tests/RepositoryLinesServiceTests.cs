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
public class RepositoryLinesServiceTests
{
    [Theory]
    [InlineData("pt-BR")] [InlineData("es")] [InlineData("en")]
    public async Task ValidatesLinesTokensAndScopesAndDelegatesOnlyPersistedRoots(string language)
    {
        RequestLanguage.Current = language;
        var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>(); var links = new Mock<IRepositoryLinkRepository>(); var editor = new Mock<IRepositoryLineEditor>();
        var service = new RepositoryLinesService(catalog.Object, workspaces.Object, links.Object, editor.Object); var file = new RepositoryLinesRequest(1, "file.txt", "working"); var token = new string('A', 64);
        foreach (var invalid in new[] { file with { RepositoryId = 0 }, file with { WorkspaceId = -1 }, file with { Area = "HEAD" }, file with { Path = "" }, file with { Path = null! }, file with { Path = "\0" }, file with { Path = new string('x', 2001) } }) await Assert.ThrowsAsync<ValidationAppException>(() => service.ReadAsync(invalid, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file, default)); await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file with { WorkspaceId = 2 }, default));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file with { WorkspaceId = 2 }, default));
        var repo = new GitRepository { Id = 1, Name = "repo", LocalPath = "stored-root" }; catalog.Setup(x => x.FindAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(repo); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([repo]);
        var view = new RepositoryLinesSnapshot(new(file.Path, file.Area, "text", "+code"), token, true, ""); var request = new PrepareLinesRequest(file, [1, 2], token); var plan = new RepositoryLinesPlan(token, file.Path, file.Area, 2, "code", false);
        editor.Setup(x => x.ReadAsync("stored-root", It.IsAny<RepositoryLinesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(view);
        editor.Setup(x => x.PrepareAsync("stored-root", request, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        editor.Setup(x => x.ExecuteAsync("stored-root", It.IsAny<ExecuteLinesRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Assert.Same(view, (await service.ReadAsync(file with { WorkspaceId = 2, Area = "staged" }, default)).Result); Assert.Same(plan, (await service.PrepareAsync(request, default)).Result); Assert.True((await service.ExecuteAsync(new(request, token), default)).Result);
        foreach (var invalid in new[] { request with { Operation = "shell" }, request with { Operation = "discard", File = file with { Area = "staged" } }, request with { File = null! }, request with { Lines = null! }, request with { Lines = [] }, request with { Lines = new int[4001] }, request with { Lines = [-1] }, request with { Lines = [10000] }, request with { Lines = [1, 1] }, request with { Token = null! }, request with { Token = "bad" }, request with { Token = new string('Z', 64) } })
        {
            await Assert.ThrowsAsync<ValidationAppException>(() => service.PrepareAsync(invalid, default)); await Assert.ThrowsAsync<ValidationAppException>(() => service.ExecuteAsync(new(invalid, token), default));
        }
        foreach (var invalid in new[] { new ExecuteLinesRequest(null!, token), new(request, "bad"), new(request, null!), new(request, new string('Z', 64)) }) await Assert.ThrowsAsync<ValidationAppException>(() => service.ExecuteAsync(invalid, default));
        foreach (var key in new[] { "lines.invalid", "lines.stale", "lines.failed", "lines.unsupported", "lines.completed" }) Assert.NotEqual(key, LocalizedText.Get(key));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ControllersPreserveCustomResponseAndStatus(bool success)
    {
        var service = new Mock<IRepositoryLinesService>(); var file = new RepositoryLinesRequest(1, "file.txt", "working"); var request = new PrepareLinesRequest(file, [1], "token"); var execute = new ExecuteLinesRequest(request, "review");
        service.Setup(x => x.ReadAsync(file, default)).ReturnsAsync(new CustomResponse<RepositoryLinesSnapshot> { Success = success });
        service.Setup(x => x.PrepareAsync(request, default)).ReturnsAsync(new CustomResponse<RepositoryLinesPlan> { Success = success });
        service.Setup(x => x.ExecuteAsync(execute, default)).ReturnsAsync(new CustomResponse<bool> { Success = success });
        var controller = new RepositoryLinesController(service.Object);
        foreach (var result in new[] { await controller.GetSelection(file, default), await controller.Prepare(request, default), await controller.Execute(execute, default) }) Assert.Equal(success ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result.GetType());
    }
}
