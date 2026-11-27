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
public class RepositoryConflictServiceTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ValidatesSelectionAndResolutionAndResolvesOnlyCatalogOrLinkedPaths(string language)
    {
        RequestLanguage.Current = language; var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>(); var links = new Mock<IRepositoryLinkRepository>(); var conflicts = new Mock<IRepositoryConflicts>();
        var service = new RepositoryConflictService(catalog.Object, workspaces.Object, links.Object, conflicts.Object); var file = new RepositoryConflictRequest(1, "file.txt"); var token = new string('A', 64);
        foreach (var invalid in new[] { file with { RepositoryId = 0 }, file with { WorkspaceId = -1 }, file with { Path = "" }, file with { Path = null! }, file with { Path = "\0" }, file with { Path = new string('x', 2001) } }) await Assert.ThrowsAsync<ValidationAppException>(() => service.ReadAsync(invalid, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file, default)); await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file with { WorkspaceId = 2 }, default));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.ReadAsync(file with { WorkspaceId = 2 }, default));
        var repo = new GitRepository { Id = 1, Name = "Repo", LocalPath = "catalog-path" }; catalog.Setup(x => x.FindAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(repo); links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([repo]);
        var version = new ConflictVersion("text", "code", token, "100644"); var snapshot = new RepositoryConflict("file.txt", "merge", token, version, version, version, version); var request = new ResolveConflictRequest(file, "edit", "resolved", token); var plan = new ConflictResolutionPlan(token, file.Path, "edit", false, "text", "resolved");
        conflicts.Setup(x => x.ReadAsync("catalog-path", file.Path, It.IsAny<CancellationToken>())).ReturnsAsync(snapshot); conflicts.Setup(x => x.PrepareAsync("catalog-path", request, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        conflicts.Setup(x => x.ResolveAsync("catalog-path", It.IsAny<ResolveConflictRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); conflicts.Setup(x => x.ExternalAsync("catalog-path", file, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Assert.Same(snapshot, (await service.ReadAsync(file with { WorkspaceId = 2 }, default)).Result); Assert.Same(plan, (await service.PrepareAsync(request, default)).Result); Assert.True((await service.ResolveAsync(request, default)).Result); Assert.True((await service.ExternalAsync(file, default)).Result);
        foreach (var invalid in new[] { request with { File = null! }, request with { Action = "shell" }, request with { Content = null! }, request with { Content = new string('x', 200001) }, request with { Content = "\0" }, request with { Token = null! }, request with { Token = "bad" }, request with { Token = new string('z', 64) } }) await Assert.ThrowsAsync<ValidationAppException>(() => service.PrepareAsync(invalid, default));
        foreach (var action in new[] { "ours", "theirs", "delete", "stage" }) Assert.True((await service.ResolveAsync(request with { Action = action }, default)).Success);
        foreach (var key in new[] { "conflict.invalid", "conflict.stale", "conflict.failed", "conflict.markers", "conflict.operation", "conflict.limit" }) Assert.NotEqual(key, LocalizedText.Get(key));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesCustomResponseAndResponseStatus(bool success)
    {
        var service = new Mock<IRepositoryConflictService>(); var file = new RepositoryConflictRequest(1, "file.txt"); var request = new ResolveConflictRequest(file, "edit");
        service.Setup(x => x.ReadAsync(file, default)).ReturnsAsync(new CustomResponse<RepositoryConflict> { Success = success }); service.Setup(x => x.PrepareAsync(request, default)).ReturnsAsync(new CustomResponse<ConflictResolutionPlan> { Success = success });
        service.Setup(x => x.ResolveAsync(request, default)).ReturnsAsync(new CustomResponse<bool> { Success = success }); service.Setup(x => x.ExternalAsync(file, default)).ReturnsAsync(new CustomResponse<bool> { Success = success });
        var controller = new RepositoryConflictController(service.Object);
        foreach (var result in new[] { await controller.GetConflict(file, default), await controller.Prepare(request, default), await controller.Resolve(request, default), await controller.External(file, default) }) Assert.Equal(success ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result.GetType());
    }
}
