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

public class RepositoryBranchServiceTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ValidatesNamesScopesTokensAndLocalizesResults(string language)
    {
        RequestLanguage.Current = language;
        var catalog = new Mock<IRepositoryCatalogRepository>(); var workspaces = new Mock<IWorkspaceRepository>();
        var links = new Mock<IRepositoryLinkRepository>(); var branches = new Mock<IRepositoryBranches>();
        var service = new RepositoryBranchService(catalog.Object, workspaces.Object, links.Object, branches.Object);
        var request = new RepositoryBranchRequest(1, 0, "create", "main", "feature/new");
        foreach (var invalid in new[] { request with { RepositoryId = 0 }, request with { WorkspaceId = -1 }, request with { Operation = "reset" }, request with { Name = null! }, request with { Name = " " }, request with { Name = "-bad" }, request with { Name = "HEAD" }, request with { Name = "bad\0name" }, request with { Name = new string('n', 256) }, request with { NewName = "" }, request with { NewName = "\n" }, request with { Operation = "delete" }, request with { Operation = "checkout" } })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.PrepareAsync(invalid));
        foreach (var invalid in new[] {
            request with { Remote = null! }, request with { Remote = "-bad" }, request with { Remote = "bad\n" }, request with { Remote = new string('r', 256) },
            request with { RemoteBranch = null! }, request with { RemoteBranch = "bad\0" }, request with { RemoteBranch = new string('r', 256) },
            request with { Upstream = null! }, request with { Upstream = "bad\n" }, request with { Upstream = new string('r', 512) },
            request with { BaseBranch = null! }, request with { BaseBranch = "bad\n" }, request with { BaseBranch = new string('r', 256) },
            request with { Destination = null! }, request with { Destination = "bad\n" }, request with { Destination = new string('r', 3001) },
            request with { Message = null! }, request with { Message = "bad\0" }, request with { Message = new string('r', 10001) },
            request with { Confirmation = null! }, request with { Confirmation = "bad\n" }, request with { Confirmation = new string('r', 512) },
            request with { Steps = Enumerable.Repeat(new RebaseStep(new string('a', 40), "pick"), 201).ToArray() }, request with { Steps = [null!] },
            request with { Steps = [new(null!, "pick")] }, request with { Steps = [new("bad", "pick")] }, request with { Steps = [new(new string('z', 40), "pick")] }, request with { Steps = [new(new string('a', 40), "exec")] },
            request with { Operation = "checkoutRemote", NewName = "" }, request with { Operation = "worktree", IsRemote = true, NewName = "" } })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.PrepareAsync(invalid));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(new(0)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAsync(new(1, -1)));
        foreach (var invalid in new[] { new ExecuteRepositoryBranchRequest(null!, "bad"), new(request, null!), new(request, "bad"), new(request, new string('z', 64)), new(request with { Name = "" }, new string('A', 64)) })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.ExecuteAsync(invalid));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1)));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1, 2)));
        workspaces.Setup(x => x.ExistsAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(new(1, 2)));
        var repository = new GitRepository { Id = 1, Name = "Repo", LocalPath = "stored" }; using var source = new CancellationTokenSource();
        catalog.Setup(x => x.FindAsync(1, source.Token)).ReturnsAsync(repository);
        links.Setup(x => x.GetAllAsync(2, source.Token)).ReturnsAsync([repository]);
        var snapshot = new LocalGitBranches("main", false, true, []);
        branches.Setup(x => x.ReadAsync("stored", source.Token)).ReturnsAsync(snapshot);
        var resources = new BranchResources("main", "", ["origin"], []);
        branches.Setup(x => x.ReadResourcesAsync("stored", source.Token)).ReturnsAsync(resources);
        Assert.Same(resources, (await service.GetResourcesAsync(new(1), source.Token)).Result);
        Assert.Same(snapshot, (await service.GetAsync(new(1), source.Token)).Result);
        Assert.Same(snapshot, (await service.GetAsync(new(1, 2), source.Token)).Result);
        var plan = new RepositoryBranchPlan(new string('A', 64), "create", "main", "feature/new", new string('a', 40));
        foreach (var operation in new[] { "create", "rename", "delete", "checkout", "worktree", "fastForward", "push", "pullRequest", "merge", "rebase", "interactiveRebase", "tag", "setUpstream", "unsetUpstream", "fetch", "checkoutRemote", "deleteRemote", "pull", "continue", "abort" })
        {
            var valid = request with { WorkspaceId = 2, Operation = operation, NewName = operation is "create" or "rename" or "tag" or "checkoutRemote" ? "new" : "", Remote = "origin", RemoteBranch = "feature", Upstream = "refs/remotes/origin/feature", BaseBranch = "main", Destination = "C:/worktree", Message = "message", Confirmation = "feature", Steps = [new(new string('a', 40), "pick")] };
            branches.Setup(x => x.PrepareAsync("stored", valid, source.Token)).ReturnsAsync(plan);
            Assert.Same(plan, (await service.PrepareAsync(valid, source.Token)).Result);
        }
        var execute = new ExecuteRepositoryBranchRequest(request, plan.Token);
        branches.Setup(x => x.ExecuteAsync("stored", execute, source.Token)).ReturnsAsync("feature/new");
        var result = await service.ExecuteAsync(execute, source.Token);
        Assert.Equal("feature/new", result.Result); Assert.Equal(LocalizedText.Get("branch.completed"), Assert.Single(result.Messages));
        foreach (var key in new[] { "invalid", "missing", "exists", "checked_out", "checkout_blocked", "unmerged", "failed" })
            Assert.False(string.IsNullOrWhiteSpace(LocalizedText.Get("branch." + key)));
        branches.Setup(x => x.ReadAsync("stored", source.Token)).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(new(1), source.Token));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ControllersPreserveResponseEnvelopesAndCancellation(bool success)
    {
        var service = new Mock<IRepositoryBranchService>(); using var source = new CancellationTokenSource();
        var query = new RepositoryBranchQuery(1); var request = new RepositoryBranchRequest(1, 0, "create", "main", "new");
        var execute = new ExecuteRepositoryBranchRequest(request, new string('A', 64));
        var list = new CustomResponse<LocalGitBranches> { Success = success }; var plan = new CustomResponse<RepositoryBranchPlan> { Success = success }; var result = new CustomResponse<string> { Success = success };
        var resources = new CustomResponse<BranchResources> { Success = success }; service.Setup(x => x.GetResourcesAsync(query, source.Token)).ReturnsAsync(resources);
        service.Setup(x => x.GetAsync(query, source.Token)).ReturnsAsync(list); service.Setup(x => x.PrepareAsync(request, source.Token)).ReturnsAsync(plan); service.Setup(x => x.ExecuteAsync(execute, source.Token)).ReturnsAsync(result);
        var controller = new RepositoryBranchController(service.Object);
        var resourceResponse = Assert.IsAssignableFrom<ObjectResult>(await controller.GetResources(query, source.Token)); Assert.Same(resources, resourceResponse.Value); Assert.Equal(success ? 200 : 400, resourceResponse.StatusCode);
        var responses = new[] { (Assert.IsAssignableFrom<ObjectResult>(await controller.GetBranches(query, source.Token)), (object)list), (Assert.IsAssignableFrom<ObjectResult>(await controller.Prepare(request, source.Token)), (object)plan), (Assert.IsAssignableFrom<ObjectResult>(await controller.Execute(execute, source.Token)), (object)result) };
        foreach (var (response, expected) in responses) { Assert.Same(expected, response.Value); Assert.Equal(success ? 200 : 400, response.StatusCode); }
    }
    [Fact]
    public async Task DependencyInjectionResolvesBranchServiceAndGitAdapter()
    {
        await using var app = ApiTestHost.Build(); using var scope = app.Services.CreateScope();
        Assert.IsType<RepositoryBranchService>(scope.ServiceProvider.GetRequiredService<IRepositoryBranchService>());
        Assert.IsType<RepositoryBranches>(scope.ServiceProvider.GetRequiredService<IRepositoryBranches>());
    }
}
