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
using GitCosmos.Infrastructure.Git;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryCloneTests
{
    private const string Remote = "https://github.com/RafaelKampa/GitCosmosTest2.git";

    [Theory]
    [InlineData("https://github.com/org/repo.git", "repo")]
    [InlineData("https://github.com/org/repo", "repo")]
    [InlineData("git@github.com:org/repo.git", "repo")]
    [InlineData("ssh://git@github.com/org/repo.git", "repo")]
    [InlineData("http://localhost/repo.git/", "repo")]
    [InlineData("git://example.com/org/repo.git", "repo")]
    [InlineData("https://example.com/my%20repo.git", "my repo")]
    public void Planner_DerivesTheNewFolderWithoutCreatingIt(string url, string folder)
    {
        var root = Directory.CreateTempSubdirectory("GitCosmos-clone-plan-");
        try
        {
            var cloner = Cloner(Mock.Of<IGitCloneRunner>(), Mock.Of<IGitRepositoryInspector>());
            var plan = cloner.Plan(url, root.FullName);
            Assert.Equal(folder, plan.FolderName);
            Assert.Equal(Path.Combine(root.FullName, folder), plan.DestinationPath);
            Assert.False(Directory.Exists(plan.DestinationPath));
            Directory.CreateDirectory(plan.DestinationPath);
            Assert.Throws<ConflictAppException>(() => cloner.Plan(url, root.FullName));
            Directory.Delete(plan.DestinationPath);
            File.WriteAllText(plan.DestinationPath, "keep");
            Assert.Throws<ConflictAppException>(() => cloner.Plan(url, root.FullName));
            Assert.Equal("keep", File.ReadAllText(plan.DestinationPath));
            File.Delete(plan.DestinationPath);
        }
        finally { Directory.Delete(root.FullName); }
    }

    [Theory]
    [InlineData("ext::sh")]
    [InlineData("file:/repo.git")]
    [InlineData("git:///repo.git")]
    [InlineData("ssh://-bad/repo.git")]
    [InlineData("ssh://git:secret@example.com/repo.git")]
    [InlineData("https://example.com/name%20.git")]
    [InlineData("https://example.com/in\nvalid.git")]
    [InlineData("file:///C:/repo.git")]
    [InlineData("C:/repo.git")]
    [InlineData("-option")]
    [InlineData("https://user:token@example.com/repo.git")]
    [InlineData("https://example.com/repo.git?token=secret")]
    [InlineData("https://example.com/repo.git#branch")]
    [InlineData("https://example.com/.git")]
    [InlineData("https://example.com/CON.git")]
    [InlineData("https://example.com/name..git")]
    [InlineData("https://example.com/a%2Fb.git")]
    [InlineData("https://example.com/a%0Ab.git")]
    [InlineData("ftp://example.com/repo.git")]
    [InlineData("https://example.com/a b.git")]
    public void Planner_RejectsUnsafeOrInvalidSourceNames(string remote)
    {
        var cloner = Cloner(Mock.Of<IGitCloneRunner>(), Mock.Of<IGitRepositoryInspector>());
        Assert.Throws<ValidationAppException>(() => cloner.Plan(remote, Path.GetTempPath()));
    }

    [Fact]
    public async Task Clone_UsesOwnedStagingPublishesOnlyOnSuccessAndPreservesExistingFolders()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var runner = new Mock<IGitCloneRunner>(MockBehavior.Strict);
        runner.Setup(x => x.CloneAsync(Remote, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string destination, CancellationToken _) =>
            {
                await fixture.RunAsync(fixture.Root, "clone", "--template=", "--", fixture.RepositoryPath, destination);
                return new GitCommandResult(0, "");
            });
        var inspector = new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10)));
        var cloner = Cloner(runner.Object, inspector);
        Assert.Throws<ValidationAppException>(() => cloner.Plan(Remote, "relative"));
        Assert.Throws<ValidationAppException>(() => cloner.Plan(Remote, Path.Combine(fixture.Root, "missing")));
        var plan = cloner.Plan(Remote, fixture.Root);
        Assert.Equal(Path.Combine(fixture.Root, "GitCosmosTest2"), plan.DestinationPath);
        await Assert.ThrowsAsync<ValidationAppException>(() => cloner.CloneAsync(plan with { DestinationPath = "other" }));
        var result = await cloner.CloneAsync(plan);
        Assert.Equal(plan.DestinationPath, result.LocalPath);
        Assert.True(Directory.Exists(Path.Combine(plan.DestinationPath, ".git")));
        Assert.Empty(Directory.GetDirectories(fixture.Root, ".gitcosmos-clone-*"));
        await Assert.ThrowsAsync<ConflictAppException>(() => cloner.CloneAsync(plan));
        var nativeDestination = Path.Combine(fixture.Root, "native-clone");
        Assert.Equal(0, (await new GitCloneRunner().CloneAsync(fixture.RepositoryPath, nativeDestination)).ExitCode);
        Assert.True(Directory.Exists(Path.Combine(nativeDestination, ".git")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clone_FailureOrCancellationRemovesOnlyItsTemporaryFiles(bool cancel)
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var runner = new Mock<IGitCloneRunner>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        runner.Setup(x => x.CloneAsync(Remote, It.IsAny<string>(), source.Token))
            .Returns((string _, string destination, CancellationToken _) =>
            {
                File.WriteAllText(Path.Combine(destination, "partial"), "partial");
                if (cancel) { source.Cancel(); throw new OperationCanceledException(source.Token); }
                return Task.FromResult(new GitCommandResult(128, "private diagnostic"));
            });
        var cloner = Cloner(runner.Object, Mock.Of<IGitRepositoryInspector>());
        var plan = cloner.Plan(Remote, fixture.Root);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cloner.CloneAsync(plan, source.Token));
        else await Assert.ThrowsAsync<OperationFailedAppException>(() => cloner.CloneAsync(plan, source.Token));
        Assert.False(Directory.Exists(plan.DestinationPath));
        Assert.Empty(Directory.GetDirectories(fixture.Root, ".gitcosmos-clone-*"));
        Assert.True(Directory.Exists(fixture.RepositoryPath));
    }

    [Fact]
    public async Task Clone_DoesNotOverwriteADestinationCreatedWhileCloning()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var runner = new Mock<IGitCloneRunner>(MockBehavior.Strict);
        var cloner = Cloner(runner.Object, Mock.Of<IGitRepositoryInspector>());
        var plan = cloner.Plan(Remote, fixture.Root);
        runner.Setup(x => x.CloneAsync(Remote, It.IsAny<string>(), default)).Returns(() =>
        {
            Directory.CreateDirectory(plan.DestinationPath);
            File.WriteAllText(Path.Combine(plan.DestinationPath, "keep"), "keep");
            return Task.FromResult(new GitCommandResult(0, ""));
        });
        await Assert.ThrowsAsync<ConflictAppException>(() => cloner.CloneAsync(plan));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(plan.DestinationPath, "keep")));
        Assert.Empty(Directory.GetDirectories(fixture.Root, ".gitcosmos-clone-*"));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Service_ValidatesPreviewsConfirmsAndLinksWithLocalizedResults(string language)
    {
        RequestLanguage.Current = language;
        var workspaces = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var links = new Mock<IRepositoryLinkRepository>(MockBehavior.Strict);
        var cloner = new Mock<IRepositoryCloner>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        var plan = new RepositoryClonePlan(Remote, @"C:\Repos", "GitCosmosTest2", @"C:\Repos\GitCosmosTest2");
        var service = new RepositoryCloneService(workspaces.Object, links.Object, cloner.Object);
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(false);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.PreviewAsync(new(1, Remote, plan.ParentPath), source.Token));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.PreviewAsync(new(0, "", ""), source.Token));
        await Assert.ThrowsAsync<ValidationAppException>(() => service.PreviewAsync(new(1, new string('x', 4097), new string('x', 32768)), source.Token));
        workspaces.Setup(x => x.ExistsAsync(1, source.Token)).ReturnsAsync(true);
        cloner.Setup(x => x.Plan(Remote, plan.ParentPath)).Returns(plan);
        Assert.Equal(plan, (await service.PreviewAsync(new(1, Remote, plan.ParentPath), source.Token)).Result);
        var request = new CloneWorkspaceRepositoryRequest(1, Remote, plan.ParentPath, plan.DestinationPath);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.CloneAsync(request with { DestinationPath = "changed" }, source.Token));
        cloner.Setup(x => x.CloneAsync(plan, source.Token)).ReturnsAsync(new LocalGitRepository(plan.FolderName, plan.DestinationPath, plan.DestinationPath.ToUpperInvariant()));
        var repository = new GitRepository { Id = 5, Name = plan.FolderName, LocalPath = plan.DestinationPath };
        links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), source.Token)).ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.Added, repository));
        var result = await service.CloneAsync(request, source.Token);
        Assert.Equal(5, result.Result!.Id);
        Assert.Equal(LocalizedText.Get("clone.completed", plan.DestinationPath), Assert.Single(result.Messages));
        links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), source.Token)).ReturnsAsync(new RepositoryLinkResult(RepositoryLinkStatus.WorkspaceNotFound));
        var missing = await Assert.ThrowsAsync<ConflictAppException>(() => service.CloneAsync(request, source.Token));
        Assert.Contains(plan.DestinationPath, missing.Message);
        links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), source.Token)).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<OperationFailedAppException>(() => service.CloneAsync(request, source.Token));
        links.Setup(x => x.AddAsync(1, It.IsAny<GitRepository>(), source.Token)).ThrowsAsync(new OperationCanceledException());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CloneAsync(request, source.Token));
    }

    [Fact]
    public async Task Controller_PreservesBusinessEnvelopesAndCancellation()
    {
        var service = new Mock<IRepositoryCloneService>(MockBehavior.Strict);
        var controller = new RepositoryCloneController(service.Object);
        var preview = new PreviewRepositoryCloneRequest(1, Remote, "parent");
        var confirm = new CloneWorkspaceRepositoryRequest(1, Remote, "parent", "destination");
        var plan = CustomResponse<RepositoryClonePlan>.Ok(new(Remote, "parent", "name", "destination"));
        service.Setup(x => x.PreviewAsync(preview, default)).ReturnsAsync(plan);
        Assert.Same(plan, Assert.IsType<OkObjectResult>(await controller.Preview(preview, default)).Value);
        service.Setup(x => x.PreviewAsync(preview, default)).ReturnsAsync(CustomResponse<RepositoryClonePlan>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.Preview(preview, default));
        service.Setup(x => x.CloneAsync(confirm, default)).ReturnsAsync(CustomResponse<WorkspaceRepositoryResponse>.Ok(new(1, "name", "destination")));
        Assert.IsType<OkObjectResult>(await controller.CloneRepository(confirm, default));
        service.Setup(x => x.CloneAsync(confirm, default)).ReturnsAsync(CustomResponse<WorkspaceRepositoryResponse>.Fail("invalid"));
        Assert.IsType<BadRequestObjectResult>(await controller.CloneRepository(confirm, default));
    }

    private static RepositoryCloner Cloner(IGitCloneRunner runner, IGitRepositoryInspector inspector) =>
        new(runner, inspector, NullLogger<RepositoryCloner>.Instance);
}
