using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryEditorTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string NewSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "GitCosmos-editor-fake");
    private static GitFileChange ChangedFile(string path = "file.txt", string index = "M", string working = "M") => new(path, null, index, working, false, false, false);
    private static GitChangesSnapshot Snapshot(params GitFileChange[] files) => new(new("main", false, true, files.Length, 1, 0, 0, 0, 0, 0, 1, 1), files);
    private static RepositoryEditRequest Request(string operation = "stage", bool amend = false, bool push = false) => new(1, 0, operation, operation == "commit" ? [] : ["file.txt"], "Subject", "Body", amend, push);

    private sealed class Fixture
    {
        public Mock<IBoundedGitProcessRunner> Reader { get; } = new();
        public Mock<IGitMutationRunner> Writer { get; } = new();
        public Mock<IGitRepositoryInspector> Inspector { get; } = new();
        public Mock<IRepositoryChangesReader> Changes { get; } = new();
        public RepositoryEditor Editor { get; }
        public Fixture()
        {
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root, Root));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Read("--git-common-dir", Root); Read("--absolute-git-dir", Path.Combine(Root, ".git"));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, Sha + "\n", false));
            Read("ls-files", "index-a"); Read("--format=%(upstream:remotename)%00%(upstream:remoteref)", "origin\0refs/heads/main\n");
            Read("get-url", "https://example.invalid/repo.git\n");
            Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile()));
            Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Editor = new(Reader.Object, Writer.Object, Inspector.Object, Changes.Object);
        }
        public void Read(string flag, string output, int exit = 0, bool truncated = false) => Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains(flag)), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(exit, output, truncated));
    }

    [Fact]
    public async Task PreflightDoesNotMutateAndTokensRejectChangedIndexHeadAndRequests()
    {
        var fixture = new Fixture(); var request = Request(); var plan = await fixture.Editor.PrepareAsync(Root, request);
        Assert.Equal(64, plan.Token.Length); Assert.Equal(1, plan.FileCount); Assert.Equal(new[] { "file.txt" }, plan.Paths);
        fixture.Writer.VerifyNoOtherCalls();
        fixture.Read("ls-files", "index-changed");
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.ExecuteAsync(Root, new(request, plan.Token)));
        fixture.Read("ls-files", "index-a");
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.ExecuteAsync(Root, new(request with { Description = "changed" }, plan.Token)));
        fixture.Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, NewSha, false));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.ExecuteAsync(Root, new(request, plan.Token)));
        fixture.Writer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("stage", false, "add")]
    [InlineData("unstage", false, "restore")]
    [InlineData("unstage", true, "rm")]
    public async Task StageAndUnstageTouchOnlyLiteralIndexPaths(string operation, bool unborn, string command)
    {
        var fixture = new Fixture(); var request = Request(operation);
        if (unborn) fixture.Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "", false));
        var plan = await fixture.Editor.PrepareAsync(Root, request); var result = await fixture.Editor.ExecuteAsync(Root, new(request, plan.Token));
        Assert.False(result.Committed);
        fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains(command) && a.Contains("--literal-pathspecs") && a[a.Count - 2] == "--" && a[a.Count - 1] == "file.txt"), It.IsAny<CancellationToken>()), Times.Once);
        if (command == "restore") fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--staged") && !a.Contains("--worktree")), It.IsAny<CancellationToken>()));
        if (command == "rm") fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--cached")), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task RenameUnstageIncludesBothPathsButWorkingEditOfAStagedRenameOnlyStagesTheNewPath()
    {
        var fixture = new Fixture(); fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile() with { OriginalPath = "old.txt", IndexStatus = "R" }));
        Assert.Equal(new[] { "file.txt", "old.txt" }, (await fixture.Editor.PrepareAsync(Root, Request("unstage"))).Paths);
        Assert.Equal(new[] { "file.txt" }, (await fixture.Editor.PrepareAsync(Root, Request())).Paths);
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile() with { OriginalPath = "old.txt", WorkTreeStatus = "R" }));
        Assert.Equal(2, (await fixture.Editor.PrepareAsync(Root, Request())).Paths.Count);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData(".git/config")]
    [InlineData("file:stream")]
    public async Task UnsafePathsAreRejectedBeforeWriting(string path)
    {
        var fixture = new Fixture(); fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile(path)));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request() with { Paths = [path] })); fixture.Writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectsConflictsSubmodulesEmptySelectionsDetachedCommitAndMissingAmendParent()
    {
        var fixture = new Fixture();
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile() with { IsConflicted = true }));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile() with { IsSubmodule = true }));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot());
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request("commit")));
        Assert.Equal(0, (await fixture.Editor.PrepareAsync(Root, Request("commit", true))).FileCount);
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile()) with { Status = Snapshot().Status with { IsDetached = true } });
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request("commit")));
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(Snapshot(ChangedFile()));
        fixture.Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "", false));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request("commit", true)));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task ReadFailuresAndInvalidHeadAreNotIgnored(int exit, bool truncated)
    {
        var fixture = new Fixture(); fixture.Read("ls-files", "", exit, truncated);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        fixture.Read("ls-files", "index-a");
        fixture.Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(exit, "not-a-sha", truncated));
        if (exit == 0 || truncated) await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
    }

    [Fact]
    public async Task RejectsMovedRootsAndInProgressOperationsAndHonorsCancellation()
    {
        var fixture = new Fixture(); fixture.Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root + "-moved", Root));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        fixture = new Fixture(); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Editor.PrepareAsync(Root, Request(), source.Token));
        var state = Directory.CreateTempSubdirectory("GitCosmos-operation-state-");
        try
        {
            File.WriteAllText(Path.Combine(state.FullName, "MERGE_HEAD"), Sha); fixture.Read("--absolute-git-dir", state.FullName);
            await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Editor.PrepareAsync(Root, Request()));
        }
        finally { File.Delete(Path.Combine(state.FullName, "MERGE_HEAD")); state.Delete(); }
    }

    [Theory]
    [InlineData("")]
    [InlineData(".\0refs/heads/main")]
    [InlineData("-invalid\0refs/heads/main")]
    [InlineData("origin\0refs/tags/v1")]
    public async Task CommitAndPushRequiresAnExplicitSingleUpstream(string upstream)
    {
        var fixture = new Fixture(); fixture.Read("--format=%(upstream:remotename)%00%(upstream:remoteref)", upstream);
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request("commit", push: true)));
        fixture = new Fixture(); fixture.Read("get-url", "url-one\nurl-two\n");
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Editor.PrepareAsync(Root, Request("commit", push: true)));
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("exit")]
    [InlineData("cancel")]
    [InlineData("error")]
    public async Task PublishedAmendAndPushPreserveTheLocalCommitOnRemoteFailure(string push)
    {
        var fixture = new Fixture(); fixture.Read("--contains=" + Sha, "refs/remotes/origin/main\n");
        fixture.Writer.Setup(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("push")), It.IsAny<CancellationToken>()))
            .Returns(() => push switch { "cancel" => Task.FromException<LimitedGitOutput>(new OperationCanceledException()), "error" => Task.FromException<LimitedGitOutput>(new OperationFailedAppException("failure")), _ => Task.FromResult(new LimitedGitOutput(push == "ok" ? 0 : 1, "", false)) });
        var request = Request("commit", true, true); var plan = await fixture.Editor.PrepareAsync(Root, request);
        Assert.True(plan.Published); Assert.Equal("origin/main", plan.PushDestination);
        fixture.Reader.SetupSequence(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--verify")), 256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, Sha, false)).ReturnsAsync(new LimitedGitOutput(0, NewSha, false));
        var result = await fixture.Editor.ExecuteAsync(Root, new(request, plan.Token));
        Assert.True(result.Committed); Assert.Equal(NewSha, result.Head); Assert.Equal(push == "ok", result.PushSucceeded); Assert.Equal(push == "ok", result.Warning is null);
        fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--amend") && a.Contains("Body") && !a.Contains("--no-verify")), It.IsAny<CancellationToken>()));
        fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--no-force") && a.Contains(NewSha + ":refs/heads/main")), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task CommitDoesNotPushUnlessRequestedAndFailuresNeverTriggerAnotherWrite()
    {
        var fixture = new Fixture(); var request = Request("commit") with { Description = "" }; var plan = await fixture.Editor.PrepareAsync(Root, request);
        var result = await fixture.Editor.ExecuteAsync(Root, new(request, plan.Token)); Assert.True(result.Committed); Assert.False(result.PushAttempted);
        fixture.Writer.Verify(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture = new Fixture(); plan = await fixture.Editor.PrepareAsync(Root, Request());
        fixture.Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "", false));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Editor.ExecuteAsync(Root, new(Request(), plan.Token)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationOrDestinationChangeAfterCommitPreventsPushWithoutLosingLocalSuccess(bool cancel)
    {
        var fixture = new Fixture(); var request = Request("commit", push: true); var plan = await fixture.Editor.PrepareAsync(Root, request); using var source = new CancellationTokenSource();
        fixture.Writer.Setup(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("commit")), source.Token)).Returns(() => {
            if (cancel) source.Cancel(); else fixture.Read("get-url", "https://example.invalid/changed.git\n");
            return Task.FromResult(new LimitedGitOutput(0, "", false));
        });
        var result = await fixture.Editor.ExecuteAsync(Root, new(request, plan.Token), source.Token);
        Assert.True(result.Committed); Assert.False(result.PushSucceeded); Assert.NotNull(result.Warning);
        fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("push")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepositoryWritesAreSerializedUnderTheCommonDirectoryLock()
    {
        var fixture = new Fixture(); var request = Request(); var plan = await fixture.Editor.PrepareAsync(Root, request);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        fixture.Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(async () => {
            if (Interlocked.Increment(ref calls) == 1) { entered.SetResult(); await release.Task; }
            return new LimitedGitOutput(0, "", false);
        });
        var first = fixture.Editor.ExecuteAsync(Root, new(request, plan.Token)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = fixture.Editor.ExecuteAsync(Root, new(request, plan.Token)); Assert.Equal(1, Volatile.Read(ref calls)); release.SetResult();
        await Task.WhenAll(first, second); Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RealGitStageUnstageCommitAndAmendPreserveWorkingFilesIncludingUnbornRepositories()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        await fixture.RunAsync(root, "config", "user.name", "Tests"); await fixture.RunAsync(root, "config", "user.email", "test@example.invalid");
        await fixture.RunAsync(root, "config", "commit.gpgSign", "false"); await fixture.RunAsync(root, "config", "core.hooksPath", Path.Combine(fixture.Root, "no-hooks"));
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
        var editor = new RepositoryEditor(runner, new GitMutationRunner(), new GitRepositoryInspector(runner), new RepositoryChangesReader(runner, new GitRepositoryInspector(runner)));
        const string path = "[file] ç.txt"; File.WriteAllText(Path.Combine(root, path), "original\n");
        async Task<RepositoryEditResult> Apply(RepositoryEditRequest request)
        {
            var plan = await editor.PrepareAsync(root, request); return await editor.ExecuteAsync(root, new(request, plan.Token));
        }
        var stage = Request() with { Paths = [path] };
        await Apply(stage); await Apply(stage with { Operation = "unstage" });
        Assert.True(File.Exists(Path.Combine(root, path)));
        await Apply(stage); var commit = await Apply(Request("commit")); Assert.True(GitHistoryParser.IsSha(commit.Head));
        File.WriteAllText(Path.Combine(root, path), "changed\n"); await Apply(stage);
        await Apply(stage with { Operation = "unstage" }); Assert.Equal("changed\n", File.ReadAllText(Path.Combine(root, path)));
        await Apply(stage); var amended = await Apply(Request("commit", amend: true)); Assert.NotEqual(commit.Head, amended.Head);
        Assert.Equal("changed\n", File.ReadAllText(Path.Combine(root, path)));
        File.Delete(Path.Combine(root, path)); await Apply(stage); var deleted = await Apply(Request("commit")); Assert.True(deleted.Committed); Assert.False(File.Exists(Path.Combine(root, path)));
        var remote = Path.Combine(fixture.Root, "remote.git");
        await fixture.RunAsync(fixture.Root, "init", "--bare", "--quiet", "--template=", remote);
        await fixture.RunAsync(root, "remote", "add", "origin", remote); await fixture.RunAsync(root, "push", "--quiet", "-u", "origin", "HEAD");
        File.WriteAllText(Path.Combine(root, path), "push this\n"); await Apply(stage);
        var pushed = await Apply(Request("commit", push: true)); Assert.True(pushed.PushSucceeded);
        var branch = (await runner.RunAsync(root, ["symbolic-ref", "--short", "HEAD"])).Output.Trim();
        Assert.Equal(pushed.Head, (await runner.RunAsync(remote, ["rev-parse", "refs/heads/" + branch])).Output.Trim());
        await fixture.RunAsync(root, "remote", "set-url", "origin", Path.Combine(fixture.Root, "missing-remote"));
        File.WriteAllText(Path.Combine(root, path), "local commit only\n"); await Apply(stage);
        var partial = await Apply(Request("commit", push: true)); Assert.True(partial.Committed); Assert.False(partial.PushSucceeded); Assert.NotNull(partial.Warning);
        Assert.Equal(partial.Head, (await runner.RunAsync(root, ["rev-parse", "HEAD"])).Output.Trim());
    }
}
