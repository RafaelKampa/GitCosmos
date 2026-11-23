using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;
using GitCosmos.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryLinkTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Add_ValidatesInspectsAndLocalizesLinkOutcomes(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var inspector = new Mock<IGitRepositoryInspector>(MockBehavior.Strict);
        var service = new WorkspaceRepositoryService(workspaces.Object, links.Object, inspector.Object);
        var request = new AddWorkspaceRepositoryRequest(3, @"C:\repo\nested");
        workspaces.Setup(x => x.ExistsAsync(3, source.Token)).ReturnsAsync(false);
        var missing = await Assert.ThrowsAsync<NotFoundAppException>(() => service.AddAsync(request, source.Token));
        Assert.Equal(LocalizedText.Get("workspace.not_found"), missing.Message);
        inspector.VerifyNoOtherCalls();
        workspaces.Setup(x => x.ExistsAsync(3, source.Token)).ReturnsAsync(true);
        inspector.Setup(x => x.InspectAsync(request.LocalPath, source.Token))
            .ReturnsAsync(new LocalGitRepository("repo", @"C:\repo", @"C:\REPO"));
        GitRepository? candidate = null;
        var stored = new GitRepository { Id = 9, Name = "repo", LocalPath = @"C:\repo", PathKey = @"C:\REPO" };
        links.Setup(x => x.AddAsync(3, It.IsAny<GitRepository>(), source.Token))
            .Callback<int, GitRepository, CancellationToken>((_, entity, _) => candidate = entity)
            .ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Added, stored));
        var result = await service.AddAsync(request, source.Token);
        Assert.True(result.Success);
        Assert.Equal(new WorkspaceRepositoryResponse(9, "repo", @"C:\repo"), result.Result);
        Assert.Equal(@"C:\REPO", candidate!.PathKey);
        Assert.Equal(LocalizedText.Get("repository.added"), Assert.Single(result.Messages));
        links.Setup(x => x.AddAsync(3, It.IsAny<GitRepository>(), source.Token))
            .ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Duplicate));
        var duplicate = await Assert.ThrowsAsync<ConflictAppException>(() => service.AddAsync(request, source.Token));
        Assert.Equal(LocalizedText.Get("repository.already_linked"), duplicate.Message);
        links.Setup(x => x.AddAsync(3, It.IsAny<GitRepository>(), source.Token))
            .ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.WorkspaceNotFound));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.AddAsync(request, source.Token));
    }

    [Fact]
    public async Task Validation_PrecedesAnyGitOrDatabaseAccess()
    {
        var workspaces = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var inspector = new Mock<IGitRepositoryInspector>(MockBehavior.Strict);
        var service = new WorkspaceRepositoryService(workspaces.Object, links.Object, inspector.Object);
        foreach (var path in new[] { null, "", "   ", new string('x', 32768) })
            await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAsync(new(1, path!)));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.AddAsync(new(0, "valid")));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.GetAllAsync(0));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.RemoveAsync(0, 1));
        var invalidRepository = await Assert.ThrowsAsync<ValidationAppException>(() => service.RemoveAsync(1, 0));
        Assert.Contains(LocalizedText.Get("repository.invalid_id"), invalidRepository.Errors);
        workspaces.VerifyNoOtherCalls();
        links.VerifyNoOtherCalls();
        inspector.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ListAndRemove_CheckWorkspaceAndLinkButNeverInspectGit(string language)
    {
        RequestLanguage.Current = language;
        using var source = new CancellationTokenSource();
        var workspaces = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var inspector = new Mock<IGitRepositoryInspector>(MockBehavior.Strict);
        var service = new WorkspaceRepositoryService(workspaces.Object, links.Object, inspector.Object);
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(false);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAllAsync(1, source.Token));
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.RemoveAsync(1, 4, source.Token));
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(true);
        links.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync([]);
        var empty = await service.GetAllAsync(1, source.Token);
        Assert.True(empty.Success); Assert.Empty(empty.Result!); Assert.Equal(0, empty.TotalRows);
        links.Setup(x => x.GetAllAsync(1, source.Token))
            .ReturnsAsync([new GitRepository { Id = 4, Name = "repo", LocalPath = "path" }]);
        Assert.Equal("repo", Assert.Single((await service.GetAllAsync(1, source.Token)).Result!).Name);
        links.Setup(x => x.RemoveAsync(1, 4, source.Token)).ReturnsAsync(false);
        var absent = await Assert.ThrowsAsync<NotFoundAppException>(() => service.RemoveAsync(1, 4, source.Token));
        Assert.Equal(LocalizedText.Get("repository.link_not_found"), absent.Message);
        links.Setup(x => x.RemoveAsync(1, 4, source.Token)).ReturnsAsync(true);
        var removed = await service.RemoveAsync(1, 4, source.Token);
        Assert.Equal(4, removed.Result);
        Assert.Equal(LocalizedText.Get("repository.removed"), Assert.Single(removed.Messages));
        inspector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Controllers_PreserveSuccessFailureAndCancellationContracts()
    {
        using var source = new CancellationTokenSource();
        var service = new Mock<IWorkspaceRepositoryService>(MockBehavior.Strict);
        var controller = new WorkspaceRepositoryController(service.Object);
        var request = new AddWorkspaceRepositoryRequest(1, "path");
        var added = CustomResponse<WorkspaceRepositoryResponse>.Ok(new(2, "repo", "path"));
        service.Setup(x => x.AddAsync(request, source.Token)).ReturnsAsync(added);
        Assert.Same(added, Assert.IsType<OkObjectResult>(await controller.AddRepository(request, source.Token)).Value);
        service.Setup(x => x.AddAsync(request, source.Token)).ReturnsAsync(CustomResponse<WorkspaceRepositoryResponse>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.AddRepository(request, source.Token));
        var listed = CustomResponse<List<WorkspaceRepositoryResponse>>.Ok([]);
        service.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync(listed);
        Assert.Same(listed, Assert.IsType<OkObjectResult>(await controller.GetRepositories(1, source.Token)).Value);
        service.Setup(x => x.GetAllAsync(1, source.Token)).ReturnsAsync(CustomResponse<List<WorkspaceRepositoryResponse>>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.GetRepositories(1, source.Token));
        var removed = CustomResponse<int?>.Ok(2);
        service.Setup(x => x.RemoveAsync(1, 2, source.Token)).ReturnsAsync(removed);
        Assert.Same(removed, Assert.IsType<OkObjectResult>(await controller.RemoveRepository(1, 2, source.Token)).Value);
        service.Setup(x => x.RemoveAsync(1, 2, source.Token)).ReturnsAsync(CustomResponse<int?>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.RemoveRepository(1, 2, source.Token));
    }

    [Fact]
    public async Task CompositionRoot_ResolvesRepositoryManagementAndGit()
    {
        await using var app = ApiTestHost.Build();
        using var scope = app.Services.CreateScope();
        Assert.IsType<WorkspaceRepositoryService>(scope.ServiceProvider.GetRequiredService<IWorkspaceRepositoryService>());
        Assert.IsType<RepositoryLinkRepository>(scope.ServiceProvider.GetRequiredService<IRepositoryLinkRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGitRepositoryInspector>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGitProcessRunner>());
    }
}
