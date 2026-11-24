using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class BranchOperationEngineTests
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Source = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Middle = "cccccccccccccccccccccccccccccccccccccccc";
    private static string Commit(string sha, string parent) => $"{sha}\0{parent}\0Author\0test@example.invalid\02026-11-23T00:00:00Z\0Subject\0\0Body\0";
    private static RepositoryBranchRequest Request(string operation, bool remote = false) => new(1, 0, operation, remote ? "origin/feature" : "feature", operation is "create" or "tag" or "checkoutRemote" || operation == "worktree" && remote ? "new" : "", remote, "origin", "feature", Upstream: "refs/remotes/origin/main", BaseBranch: "main", Confirmation: remote ? "origin/feature" : "");
    private sealed class Fixture : IDisposable
    {
        public DirectoryInfo Folder { get; } = Directory.CreateTempSubdirectory("GitCosmos-branch-engine-");
        public string Root { get; }
        public string GitDir { get; }
        public Mock<IBoundedGitProcessRunner> Reader { get; } = new();
        public Mock<IGitMutationRunner> Writer { get; } = new();
        public BranchOperationEngine Engine { get; }
        public LocalGitBranches Local { get; set; }
        public Fixture()
        {
            Root = Directory.CreateDirectory(Path.Combine(Folder.FullName, "repo")).FullName; GitDir = Directory.CreateDirectory(Path.Combine(Root, ".git")).FullName;
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Read("remote", "origin\n"); Read("get-url", "https://github.com/owner/repo.git\n"); Read("--short", "main\n"); Read("--absolute-git-dir", GitDir); Read("--git-common-dir", GitDir);
            Read("--format=%(refname)%00%(objectname)%00%(symref)", $"refs/remotes/origin/main\0{Head}\0\nrefs/remotes/origin/feature\0{Source}\0\nrefs/remotes/origin/HEAD\0{Head}\0refs/remotes/origin/main\n");
            Read("HEAD^{commit}", Head); Read("FETCH_HEAD^{commit}", Source); Read("show-ref", "", 1); Read("ls-files", "index");
            Read("--contains=" + Head, "refs/remotes/origin/main\n"); Read("log", Commit(Middle, Source) + Commit(Head, Middle));
            Read("--format=%(refname)%00%(objectname)%00%(upstream)%00%(worktreepath)", $"refs/heads/main\0{Head}\0refs/remotes/origin/main\0{Root}\nrefs/heads/feature\0{Source}\0refs/remotes/origin/feature\0\n");
            Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Local = new("main", false, true, [new("main", Head, "refs/remotes/origin/main", true, true), new("feature", Source, "refs/remotes/origin/feature", false, false)]);
            Engine = new(Reader.Object, Writer.Object);
        }
        public void Read(string flag, string output, int exit = 0, bool truncated = false) => Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains(flag)), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(exit, output, truncated));
        public void Dispose() { foreach (var file in Directory.EnumerateFiles(Folder.FullName, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal); Folder.Delete(true); }
    }
    [Theory]
    [InlineData("create", true, "branch")]
    [InlineData("checkoutRemote", true, "switch")]
    [InlineData("worktree", false, "worktree")]
    [InlineData("worktree", true, "worktree")]
    [InlineData("fastForward", false, "update-ref")]
    [InlineData("push", false, "push")]
    [InlineData("merge", false, "merge")]
    [InlineData("rebase", false, "rebase")]
    [InlineData("tag", false, "tag")]
    [InlineData("setUpstream", false, "branch")]
    [InlineData("unsetUpstream", false, "branch")]
    [InlineData("fetch", true, "fetch")]
    [InlineData("deleteRemote", true, "push")]
    [InlineData("pull", true, "merge")]
    public async Task ReviewsEveryOperationWithoutWritingAndExecutesOnlyItsSafeGitArguments(string operation, bool remote, string command)
    {
        using var fixture = new Fixture(); var request = Request(operation, remote) with { Destination = Path.Combine(fixture.Folder.FullName, "worktree") };
        var plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default); fixture.Writer.VerifyNoOtherCalls();
        Assert.Equal(64, plan.Token.Length); Assert.Equal(64, plan.StateToken.Length); Assert.Equal(Source, plan.Sha);
        await fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default);
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains(command) && !a.Contains("--force") && !a.Contains("-D")), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        if (operation == "push") fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--no-force") && a.Contains(Source + ":refs/heads/feature")), It.IsAny<CancellationToken>()));
        if (operation == "deleteRemote") fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--force-with-lease=refs/heads/feature:" + Source) && a.Last() == ":refs/heads/feature"), It.IsAny<CancellationToken>()));
        if (operation == "pull") fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--ff-only") && a.Contains("--no-overwrite-ignore")), It.IsAny<CancellationToken>()));
    }
    [Fact]
    public async Task CurrentBranchFastForwardUsesMergeAndAnnotatedTagsPreserveTheirMessage()
    {
        using var fixture = new Fixture(); var request = Request("fastForward") with { Name = "main", Upstream = "refs/remotes/origin/feature" };
        var plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default); await fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default);
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("merge") && a.Contains("--ff-only") && a.Contains(Source)), It.IsAny<CancellationToken>()));
        request = Request("tag") with { Message = "Annotated\nmessage" }; plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default); await fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default);
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("-a") && a.Contains("Annotated\nmessage")), It.IsAny<CancellationToken>()));
        request = Request("checkoutRemote", true) with { NewName = "feature" }; plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default);
        Assert.Equal("feature", plan.Target); await fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default);
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a[0] == "switch" && !a.Contains("-c") && a.Last() == "feature"), It.IsAny<CancellationToken>()));
    }
    [Fact]
    public async Task ResourcesSkipSymbolicRemoteHeadAndProvideOnlyNamesShaAndState()
    {
        using var fixture = new Fixture(); var resources = await fixture.Engine.ResourcesAsync(fixture.Root, default);
        Assert.Equal("main", resources.CurrentBranch); Assert.Equal(2, resources.Branches.Count); Assert.DoesNotContain(resources.Branches, branch => branch.Name.EndsWith("/HEAD"));
        fixture.Read("--format=%(refname)%00%(objectname)%00%(symref)", $"refs/remotes/orphan/feature\0{Source}\0\n");
        Assert.Equal("orphan", Assert.Single((await fixture.Engine.ResourcesAsync(fixture.Root, default)).Branches).Remote);
        fixture.Read("--short", "", 1); Assert.Equal("", (await fixture.Engine.ResourcesAsync(fixture.Root, default)).CurrentBranch);
    }
    [Theory]
    [InlineData("merge", "MERGE_HEAD")]
    [InlineData("rebase", "rebase-merge")]
    public async Task PendingMergeAndRebaseRequireExplicitContinueOrAbort(string state, string marker)
    {
        using var fixture = new Fixture(); if (state == "merge") File.WriteAllText(Path.Combine(fixture.GitDir, marker), Source); else Directory.CreateDirectory(Path.Combine(fixture.GitDir, marker));
        Assert.Equal(state, (await fixture.Engine.ResourcesAsync(fixture.Root, default)).OperationState);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("merge"), default));
        foreach (var operation in new[] { "continue", "abort" })
        {
            var request = Request(operation); var plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default); await fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default);
            fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains(state) && a.Contains(operation == "continue" ? "--continue" : "--abort")), It.IsAny<CancellationToken>()));
        }
    }
    [Fact]
    public async Task RebaseResourcesRecoverTheOriginalBranchAndBoundMetadataReads()
    {
        using var fixture = new Fixture(); var directory = Directory.CreateDirectory(Path.Combine(fixture.GitDir, "rebase-merge")); fixture.Read("--short", "", 1);
        var file = Path.Combine(directory.FullName, "head-name"); File.WriteAllText(file, "refs/heads/main\n");
        Assert.Equal("main", (await fixture.Engine.ResourcesAsync(fixture.Root, default)).CurrentBranch);
        File.WriteAllText(file, new string('x', 32769)); await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Engine.ResourcesAsync(fixture.Root, default));
        File.Delete(file); directory.Delete(); File.WriteAllText(Path.Combine(fixture.GitDir, "CHERRY_PICK_HEAD"), Head);
        Assert.Equal("other", (await fixture.Engine.ResourcesAsync(fixture.Root, default)).OperationState);
    }
    [Theory]
    [InlineData("get-url", 1, false)]
    [InlineData("get-url", 0, true)]
    [InlineData("check-ref-format", 2, false)]
    [InlineData("check-ref-format", 0, true)]
    [InlineData("merge-base", 2, false)]
    [InlineData("merge-base", 0, true)]
    [InlineData("show-ref", 2, false)]
    [InlineData("show-ref", 0, true)]
    public async Task NativeFailuresAndTruncationDoNotBecomeSuccessfulPlans(string flag, int exit, bool truncated)
    {
        using var fixture = new Fixture(); fixture.Read(flag, "", exit, truncated);
        var operation = flag == "merge-base" ? "fastForward" : flag == "show-ref" ? "tag" : "push";
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request(operation), default)); fixture.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task InteractiveRebaseValidatesTheReviewedSetOrderAndFirstActionAndKeepsABaseStateToken()
    {
        using var fixture = new Fixture(); var request = Request("interactiveRebase"); var first = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default);
        Assert.True(first.Published); Assert.True(first.RewritesHistory); Assert.Equal(2, first.Commits.Count);
        var valid = request with { Steps = [new(Middle, "pick"), new(Head, "fixup")] }; var final = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, valid, default);
        Assert.Equal(first.StateToken, final.StateToken); Assert.NotEqual(first.Token, final.Token);
        await fixture.Engine.ExecuteAsync(fixture.Root, valid, final, default);
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--interactive") && a.Any(value => value.StartsWith("sequence.editor=")) && a.Contains("--no-autostash") && a.Contains("--no-update-refs")), It.IsAny<CancellationToken>()));
        foreach (var steps in new RebaseStep[][] { [], [new(Middle, "pick"), new(Middle, "pick")], [new(Source, "pick"), new(Head, "pick")], [new(Middle, "drop"), new(Head, "drop")], [new(Middle, "squash"), new(Head, "pick")] })
            await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request with { Steps = steps }, default));
        fixture.Read("log", Commit(Head, Source + " " + Middle)); await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default));
        fixture.Read("log", ""); await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default));
    }
    [Theory]
    [InlineData("resources")]
    [InlineData("ref")]
    [InlineData("sha")]
    [InlineData("symbolic")]
    [InlineData("remote")]
    public async Task MalformedResourceOutputsNeverReachWrites(string type)
    {
        using var fixture = new Fixture();
        if (type == "resources") fixture.Read("--format=%(refname)%00%(objectname)%00%(symref)", "bad");
        if (type == "ref") fixture.Read("--format=%(refname)%00%(objectname)%00%(symref)", $"refs/heads/main\0{Head}\0\n");
        if (type == "sha") fixture.Read("--format=%(refname)%00%(objectname)%00%(symref)", "refs/remotes/origin/main\0bad\0\n");
        if (type == "symbolic") fixture.Read("--short", "", 2);
        if (type == "remote") fixture.Read("remote", "-option\n");
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Engine.ResourcesAsync(fixture.Root, default)); fixture.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task RejectsInvalidRefsUnmergedTargetsDirtyTreesDuplicateNamesAndUnsafeRecovery()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("merge") with { Name = "missing" }, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("merge", true) with { Name = "missing" }, default));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("continue"), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("merge") with { Name = "main" }, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("checkoutRemote"), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("push", true), default));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("create", true) with { NewName = "MAIN" }, default));
        fixture.Read("merge-base", "", 1); await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("fastForward"), default)); fixture.Read("merge-base", "");
        fixture.Read("status", "? untracked\0"); await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("merge"), default)); fixture.Read("status", "");
        fixture.Read("check-ref-format", "", 1); await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("tag"), default)); fixture.Read("check-ref-format", "");
        fixture.Read("show-ref", ""); await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("tag"), default));
        fixture.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task ProtectsPushDestinationsUpstreamsAndRemoteDeletionAndRequiresPublishedPullRequestSource()
    {
        using var fixture = new Fixture();
        foreach (var request in new[] { Request("push") with { Remote = "unknown" }, Request("deleteRemote", true) with { Confirmation = "wrong" }, Request("pull"), Request("setUpstream") with { Upstream = "refs/remotes/origin/missing" } })
            await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default));
        fixture.Read("get-url", "https://one\nhttps://two\n"); await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("push"), default));
        fixture.Read("get-url", "https://secret@github.com/owner/repo.git\n");
        var plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("pullRequest"), default); Assert.Equal("https://github.com/owner/repo/compare/main...feature?expand=1", plan.Url); Assert.DoesNotContain("secret", plan.Url);
        Assert.Equal(plan.Url, await fixture.Engine.ExecuteAsync(fixture.Root, Request("pullRequest"), plan, default)); fixture.Writer.VerifyNoOtherCalls();
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, Request("pullRequest") with { RemoteBranch = "missing" }, default));
    }
    [Fact]
    public void DestinationValidationPreventsExistingRelativeNestedAndUnparentedWorktreePaths()
    {
        using var fixture = new Fixture(); Assert.Equal(Path.Combine(fixture.Folder.FullName, "new"), BranchOperationEngine.ValidateDestination(fixture.Root, Path.Combine(fixture.Folder.FullName, "new")));
        foreach (var value in new[] { "relative", fixture.Root, Path.Combine(fixture.Root, "inside"), Path.Combine(fixture.Folder.FullName, "missing", "new"), Path.GetPathRoot(fixture.Root)! }) Assert.Throws<ValidationAppException>(() => BranchOperationEngine.ValidateDestination(fixture.Root, value));
    }
    [Fact]
    public async Task BranchAdapterUsesTheSharedLockAndRejectsChangedAdvancedPlans()
    {
        using var fixture = new Fixture(); var inspector = new Mock<IGitRepositoryInspector>(); inspector.Setup(x => x.InspectAsync(fixture.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", fixture.Root, fixture.Root));
        var changes = new Mock<IRepositoryChangesReader>(); changes.Setup(x => x.ReadAsync(fixture.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new GitChangesSnapshot(new("main", false, true, 0, 0, 0, 0, 0, 0, 0, 0, 0), []));
        var adapter = new RepositoryBranches(fixture.Reader.Object, fixture.Writer.Object, inspector.Object, changes.Object);
        Assert.Equal(2, (await adapter.ReadResourcesAsync(fixture.Root)).Branches.Count);
        var request = Request("tag"); var plan = await adapter.PrepareAsync(fixture.Root, request);
        fixture.Read("ls-files", "changed"); await Assert.ThrowsAsync<ConflictAppException>(() => adapter.ExecuteAsync(fixture.Root, new(request, plan.Token))); fixture.Writer.VerifyNoOtherCalls();
        fixture.Read("ls-files", "index"); await adapter.ExecuteAsync(fixture.Root, new(request, plan.Token));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailuresAndCancellationNeverRetryWrites(bool cancel)
    {
        using var fixture = new Fixture(); var request = Request("merge"); var plan = await fixture.Engine.PrepareAsync(fixture.Root, fixture.Local, request, default);
        if (cancel) fixture.Writer.Setup(x => x.RunAsync(fixture.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        else fixture.Writer.Setup(x => x.RunAsync(fixture.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "private", false));
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default));
        else await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Engine.ExecuteAsync(fixture.Root, request, plan, default));
        fixture.Writer.Verify(x => x.RunAsync(fixture.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
