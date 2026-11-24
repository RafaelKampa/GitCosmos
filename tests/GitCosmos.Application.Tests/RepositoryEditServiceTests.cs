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
using GitCosmos.Infrastructure.Git;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryEditServiceTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ValidatesInputsScopeAndCancellationAndPreservesPartialSuccess(string language)
    {
        RequestLanguage.Current = language;
        var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>(); var links = new Mock<IRepositoryLinkRepository>(); var editor = new Mock<IRepositoryEditor>();
        var service = new RepositoryEditService(catalog.Object, workspaces.Object, links.Object, editor.Object);
        var valid = new RepositoryEditRequest(1, 0, "stage", ["file"]);
        foreach (var request in new[] { valid with { RepositoryId = 0 }, valid with { WorkspaceId = -1 }, valid with { Operation = "reset" }, valid with { Paths = null! }, valid with { Paths = [] }, valid with { Paths = [""] }, valid with { Paths = ["bad\0path"] }, valid with { Paths = [new string('x', 32768)] }, valid with { Paths = Enumerable.Repeat("file", 2001).ToArray() }, valid with { Subject = "\n" }, valid with { Subject = "\r" }, valid with { Subject = "\0" }, valid with { Subject = null! }, valid with { Subject = new string('s', 201) }, valid with { Description = null! }, valid with { Description = "\0" }, valid with { Description = new string('d', 20001) }, valid with { Amend = true }, valid with { Push = true }, valid with { Operation = "commit", Subject = "" } })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.PrepareAsync(request));
        foreach (var request in new[] { new ExecuteRepositoryEditRequest(null!, "a"), new(valid, null!), new(valid, "bad"), new(valid, new string('z', 64)) })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.ExecuteAsync(request));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.PrepareAsync(valid));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.PrepareAsync(valid with { WorkspaceId = 2 }));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.PrepareAsync(valid with { WorkspaceId = 2 }));
        var repository = new GitRepository { Id = 1, Name = "Repo", LocalPath = "stored" }; using var source = new CancellationTokenSource();
        catalog.Setup(x => x.FindAsync(1, source.Token)).ReturnsAsync(repository);
        links.Setup(x => x.GetAllAsync(2, source.Token)).ReturnsAsync([repository]);
        var plan = new RepositoryEditPlan(new string('A', 64), "stage", ["file"], 1, "head", false, false, null);
        editor.Setup(x => x.PrepareAsync("stored", valid, source.Token)).ReturnsAsync(plan);
        Assert.Same(plan, (await service.PrepareAsync(valid, source.Token)).Result);
        var scoped = valid with { WorkspaceId = 2 }; editor.Setup(x => x.PrepareAsync("stored", scoped, source.Token)).ReturnsAsync(plan);
        Assert.Same(plan, (await service.PrepareAsync(scoped, source.Token)).Result);
        var execute = new ExecuteRepositoryEditRequest(valid, plan.Token);
        var success = new RepositoryEditResult("stage", "head", false, false, false, null);
        editor.Setup(x => x.ExecuteAsync("stored", execute, source.Token)).ReturnsAsync(success);
        Assert.Equal(LocalizedText.Get("edit.completed"), Assert.Single((await service.ExecuteAsync(execute, source.Token)).Messages));
        editor.Setup(x => x.ExecuteAsync("stored", execute, source.Token)).ReturnsAsync(success with { Committed = true, Warning = "warning" });
        Assert.Equal(LocalizedText.Get("edit.push_failed"), Assert.Single((await service.ExecuteAsync(execute, source.Token)).Messages));
        editor.Setup(x => x.ExecuteAsync("stored", execute, source.Token)).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(execute, source.Token));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesEnvelopesAndCancellation(bool success)
    {
        var service = new Mock<IRepositoryEditService>(); var request = new RepositoryEditRequest(1, 0, "stage", ["file"]); var execute = new ExecuteRepositoryEditRequest(request, new string('A', 64));
        using var source = new CancellationTokenSource(); var plan = new CustomResponse<RepositoryEditPlan> { Success = success }; var result = new CustomResponse<RepositoryEditResult> { Success = success };
        service.Setup(x => x.PrepareAsync(request, source.Token)).ReturnsAsync(plan); service.Setup(x => x.ExecuteAsync(execute, source.Token)).ReturnsAsync(result);
        var controller = new RepositoryEditController(service.Object);
        var first = Assert.IsAssignableFrom<ObjectResult>(await controller.Prepare(request, source.Token)); var second = Assert.IsAssignableFrom<ObjectResult>(await controller.Execute(execute, source.Token));
        Assert.Same(plan, first.Value); Assert.Same(result, second.Value); Assert.Equal(success ? 200 : 400, first.StatusCode); Assert.Equal(success ? 200 : 400, second.StatusCode);
    }

    [Fact]
    public async Task DependencyInjectionResolvesEditorAndMutationRunner()
    {
        await using var app = ApiTestHost.Build(); using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryEditService>(scope.ServiceProvider.GetRequiredService<IRepositoryEditService>());
        Assert.IsType<RepositoryEditor>(scope.ServiceProvider.GetRequiredService<IRepositoryEditor>());
        Assert.IsType<GitMutationRunner>(scope.ServiceProvider.GetRequiredService<IGitMutationRunner>());
    }
}
