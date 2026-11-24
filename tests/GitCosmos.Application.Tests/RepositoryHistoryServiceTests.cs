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

public class RepositoryHistoryServiceTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ValidatesRequestsAndResolvesOnlyTheRequestedRepository(string language)
    {
        RequestLanguage.Current = language;
        var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>();
        var links = new Mock<IRepositoryLinkRepository>(); var reader = new Mock<IRepositoryHistoryReader>(MockBehavior.Strict);
        var service = new RepositoryHistoryService(catalog.Object, workspaces.Object, links.Object, reader.Object);
        foreach (var request in new RepositoryHistoryRequest[] { new(0), new(1, -1), new(1, Page: -1), new(1, Page: 10001),
            new(1, PageSize: 0), new(1, PageSize: 101), new(1, Page: 1), new(1, Snapshot: "bad"), new(1, Snapshot: new string('z', 64)),
            new(1, Branch: new string('x', 256)), new(1, Branch: "bad\0"), new(1, Search: new string('x', 201)), new(1, Search: "bad\n") })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(request));
        foreach (var request in new RepositoryCommitRequest[] { new(0, Sha), new(1, Sha, -1), new(1, "HEAD"), new(1, null!), new(1, new string('z', 40)) })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.GetCommitAsync(request));
        foreach (var path in new[] { "", new string('x', 32768), "bad\0path" })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.GetDiffAsync(new(1, Sha, path)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetDiffAsync(new(1, "HEAD", "file")));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1)));
        var missing = await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1, 2)));
        Assert.Equal(LocalizedText.Get("workspace.not_found"), missing.Message);
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetCommitAsync(new(1, Sha, 2)));
        reader.VerifyNoOtherCalls();
        using var source = new CancellationTokenSource();
        var repository = new GitRepository { Id = 1, Name = "Repo", LocalPath = "persisted path" };
        catalog.Setup(x => x.FindAsync(1, source.Token)).ReturnsAsync(repository);
        links.Setup(x => x.GetAllAsync(2, source.Token)).ReturnsAsync([repository]);
        var page = new GitHistoryPage([], new string('A', 64), false);
        var commit = new GitCommit(Sha, [], "Author", "email", "2026-11-22T00:00:00Z", "subject", "", "message");
        var details = new GitCommitDetails(commit, []); var diff = new GitFileDiff("file", "working", "empty", "");
        reader.Setup(x => x.ReadAsync("persisted path", 0, 50, null, source.Token)).ReturnsAsync(page);
        reader.Setup(x => x.ReadCommitAsync("persisted path", Sha, source.Token)).ReturnsAsync(details);
        reader.Setup(x => x.ReadDiffAsync("persisted path", Sha, "file", source.Token)).ReturnsAsync(diff);
        Assert.Same(page, (await service.GetAsync(new(1), source.Token)).Result);
        Assert.Same(page, (await service.GetAsync(new(1, 2), source.Token)).Result);
        reader.Setup(x => x.ReadFilteredAsync("persisted path", 0, 50, null, "main", "fix", source.Token)).ReturnsAsync(page);
        Assert.Same(page, (await service.GetAsync(new(1, Branch: "main", Search: "fix"), source.Token)).Result);
        reader.Setup(x => x.ReadFilteredAsync("persisted path", 0, 50, null, null, "fix", source.Token)).ReturnsAsync(page);
        Assert.Same(page, (await service.GetAsync(new(1, Search: "fix"), source.Token)).Result);
        Assert.Same(page, (await service.GetAsync(new(1, Branch: "", Search: " "), source.Token)).Result);
        Assert.Same(details, (await service.GetCommitAsync(new(1, Sha, 2), source.Token)).Result);
        Assert.Same(diff, (await service.GetDiffAsync(new(1, Sha, "file"), source.Token)).Result);
        reader.Setup(x => x.ReadAsync("persisted path", 1, 50, new string('A', 64), source.Token)).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(new(1, Page: 1, Snapshot: new string('A', 64)), source.Token));
        foreach (var key in new[] { "history.invalid", "history.failed", "history.limit", "history.changed", "history.not_found" })
            Assert.NotEqual(key, LocalizedText.Get(key));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllerForwardsContractsAndCancellation(bool success)
    {
        var service = new Mock<IRepositoryHistoryService>(); using var source = new CancellationTokenSource();
        var page = new CustomResponse<GitHistoryPage> { Success = success };
        var details = new CustomResponse<GitCommitDetails> { Success = success };
        var diff = new CustomResponse<GitFileDiff> { Success = success };
        var listRequest = new RepositoryHistoryRequest(1); var commitRequest = new RepositoryCommitRequest(1, Sha);
        var diffRequest = new RepositoryCommitDiffRequest(1, Sha, "file");
        service.Setup(x => x.GetAsync(listRequest, source.Token)).ReturnsAsync(page);
        service.Setup(x => x.GetCommitAsync(commitRequest, source.Token)).ReturnsAsync(details);
        service.Setup(x => x.GetDiffAsync(diffRequest, source.Token)).ReturnsAsync(diff);
        var controller = new RepositoryHistoryController(service.Object);
        foreach (var pair in new (IActionResult Result, object Envelope)[] {
            (await controller.GetHistory(listRequest, source.Token), page),
            (await controller.GetCommit(commitRequest, source.Token), details),
            (await controller.GetDiff(diffRequest, source.Token), diff) })
        {
            var response = Assert.IsAssignableFrom<ObjectResult>(pair.Result);
            Assert.Equal(success ? 200 : 400, response.StatusCode); Assert.Same(pair.Envelope, response.Value);
        }
    }

    [Fact]
    public async Task HostResolvesHistoryContracts()
    {
        await using var app = ApiTestHost.Build(); using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryHistoryService>(scope.ServiceProvider.GetRequiredService<IRepositoryHistoryService>());
        Assert.IsType<RepositoryHistoryReader>(scope.ServiceProvider.GetRequiredService<IRepositoryHistoryReader>());
    }
}
