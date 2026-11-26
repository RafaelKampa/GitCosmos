using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;
using Moq;
namespace GitCosmos.Application.Tests;
public class CommitExplorerServiceTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ValidatesScopeAndRequestsAndForwardsAllOperations(string language)
    {
        RequestLanguage.Current = language;
        var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>(); var links = new Mock<IRepositoryLinkRepository>(); var explorer = new Mock<ICommitExplorer>();
        var service = new CommitExplorerService(catalog.Object, workspaces.Object, links.Object, explorer.Object);
        var file = new CommitFileSelection(1, 0, Sha, "file.txt");
        await Assert.ThrowsAsync<ValidationAppException>(() => service.TreeAsync(file with { RepositoryId = 0 }, default));
        foreach (var path in new[] { null, "", "\0", new string('x', 32768) }) await Assert.ThrowsAsync<ValidationAppException>(() => service.FileAsync(file with { Path = path! }, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.TreeAsync(file, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.TreeAsync(file with { WorkspaceId = 2 }, default));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.TreeAsync(file with { WorkspaceId = 2 }, default));
        var repository = new GitRepository { Id = 1, Name = "Repo", LocalPath = "catalog-root" };
        catalog.Setup(x => x.FindAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(repository);
        links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([repository]);
        explorer.Setup(x => x.TreeAsync("catalog-root", Sha, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        explorer.Setup(x => x.FileAsync("catalog-root", Sha, "file.txt", It.IsAny<CancellationToken>())).ReturnsAsync(new CommitFileContent("file.txt", "text", "x", "eA==", "catalog-root/file.txt"));
        explorer.Setup(x => x.HistoryAsync("catalog-root", Sha, "file.txt", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        explorer.Setup(x => x.BlameAsync("catalog-root", Sha, "file.txt", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        explorer.Setup(x => x.Tools()).Returns([]);
        var restore = new CommitRestoreRequest(file); var plan = new CommitRestorePlan(new string('A', 64), "file.txt", Sha, "file.txt", true);
        explorer.Setup(x => x.PrepareAsync("catalog-root", restore, It.IsAny<CancellationToken>())).ReturnsAsync(plan);
        explorer.Setup(x => x.RestoreAsync("catalog-root", It.IsAny<CommitRestoreRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        explorer.Setup(x => x.DesktopAsync("catalog-root", It.IsAny<CommitDesktopRequest>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Assert.True((await service.TreeAsync(file with { Path = "", WorkspaceId = 2 }, default)).Success);
        Assert.Equal("x", (await service.FileAsync(file, default)).Result!.Text);
        Assert.True((await service.HistoryAsync(file, default)).Success); Assert.True((await service.BlameAsync(file, default)).Success); Assert.True((await service.ToolsAsync(file, default)).Success);
        Assert.Same(plan, (await service.PrepareAsync(restore, default)).Result);
        Assert.True((await service.RestoreAsync(restore with { Token = plan.Token }, default)).Result);
        foreach (var token in new[] { null, "", "bad", new string('Z', 64) }) await Assert.ThrowsAsync<ValidationAppException>(() => service.RestoreAsync(restore with { Token = token! }, default));
        foreach (var action in new[] { "open", "reveal", "diff" }) Assert.True((await service.DesktopAsync(new(file, action, "vscode"), default)).Result);
        Assert.True((await service.DesktopAsync(new(file, "diff", "visualstudio"), default)).Result);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.DesktopAsync(new(file, "shell"), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.DesktopAsync(new(file, "diff", "arbitrary.exe"), default));
        foreach (var key in new[] { "commit.limit", "commit.submodule", "commit.text_only", "commit.desktop_failed" }) Assert.NotEqual(key, LocalizedText.Get(key));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerPreservesCustomResponseAndFailureStatus(bool success)
    {
        var file = new CommitFileSelection(1, 0, Sha, "file"); var service = new Mock<ICommitExplorerService>();
        var snapshot = file with { Path = "" };
        service.Setup(x => x.TreeAsync(snapshot, default)).ReturnsAsync(new CustomResponse<IReadOnlyList<CommitTreeEntry>> { Success = success });
        service.Setup(x => x.FileAsync(file, default)).ReturnsAsync(new CustomResponse<CommitFileContent> { Success = success });
        service.Setup(x => x.HistoryAsync(file, default)).ReturnsAsync(new CustomResponse<IReadOnlyList<GitCommit>> { Success = success });
        service.Setup(x => x.BlameAsync(file, default)).ReturnsAsync(new CustomResponse<IReadOnlyList<CommitBlameLine>> { Success = success });
        service.Setup(x => x.ToolsAsync(snapshot, default)).ReturnsAsync(new CustomResponse<IReadOnlyList<CommitExternalTool>> { Success = success });
        var request = new CommitRestoreRequest(file); var desktop = new CommitDesktopRequest(file, "open");
        service.Setup(x => x.PrepareAsync(request, default)).ReturnsAsync(new CustomResponse<CommitRestorePlan> { Success = success });
        service.Setup(x => x.RestoreAsync(request, default)).ReturnsAsync(new CustomResponse<bool> { Success = success });
        service.Setup(x => x.DesktopAsync(desktop, default)).ReturnsAsync(new CustomResponse<bool> { Success = success });
        var controller = new CommitExplorerController(service.Object);
        foreach (var result in new[] { await controller.Tree(new(1, Sha), default), await controller.FileContent(file, default), await controller.History(file, default), await controller.Blame(file, default), await controller.Tools(new(1, Sha), default), await controller.PrepareRestore(request, default), await controller.Restore(request, default), await controller.Desktop(desktop, default) })
            Assert.Equal(success ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result.GetType());
    }
}
