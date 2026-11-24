using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryBranchesTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "GitCosmos-branches-fake");
    private static RepositoryBranchRequest Request(string operation = "create", string name = "feature/existing") => new(1, 0, operation, name, operation is "create" or "rename" ? "feature/new" : "");
    private static string Refs(string otherPath = "", string sha = Sha) => $"refs/heads/main\0{Sha}\0refs/remotes/origin/main\0{Root}\nrefs/heads/feature/existing\0{sha}\0\0{otherPath}\n";
    private sealed class Fixture
    {
        public Mock<IBoundedGitProcessRunner> Reader { get; } = new();
        public Mock<IGitMutationRunner> Writer { get; } = new();
        public Mock<IGitRepositoryInspector> Inspector { get; } = new();
        public Mock<IRepositoryChangesReader> Changes { get; } = new();
        public RepositoryBranches Branches { get; }
        public Fixture()
        {
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root, Root));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Read("for-each-ref", Refs()); Read("--git-common-dir", Root); Read("--absolute-git-dir", Path.Combine(Root, ".git")); Read("--verify", Sha);
            Status(); Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Branches = new(Reader.Object, Writer.Object, Inspector.Object, Changes.Object);
        }
        public void Read(string flag, string output, int exit = 0, bool truncated = false) => Reader.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(args => args.Contains(flag)), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(exit, output, truncated));
        public void Status(string branch = "main", bool detached = false, bool commits = true) => Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new GitChangesSnapshot(new(branch, detached, commits, 0, 0, 0, 0, 0, 0, 0, 0, 0), []));
    }
    [Fact]
    public async Task ListsCurrentLocalBranchesUpstreamsAndOtherWorktreesWithoutWriting()
    {
        var fixture = new Fixture(); fixture.Read("for-each-ref", Refs("other-worktree"));
        var list = await fixture.Branches.ReadAsync(Root); Assert.Equal("main", list.CurrentBranch);
        Assert.True(list.Branches[0].IsCurrent); Assert.Equal("refs/remotes/origin/main", list.Branches[0].Upstream);
        Assert.True(list.Branches[1].IsCheckedOut); Assert.False(list.Branches[1].IsCurrent); Assert.Null(list.Branches[1].Upstream);
        fixture.Status("HEAD", detached: true); Assert.DoesNotContain((await fixture.Branches.ReadAsync(Root)).Branches, branch => branch.IsCurrent);
        fixture.Status(commits: false); fixture.Read("for-each-ref", ""); var unborn = await fixture.Branches.ReadAsync(Root); Assert.False(unborn.HasCommits); Assert.Empty(unborn.Branches);
        fixture.Writer.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("create", "branch", "--no-track")]
    [InlineData("rename", "branch", "-m")]
    [InlineData("delete", "branch", "-d")]
    [InlineData("checkout", "switch", "--no-guess")]
    public async Task ReviewsWithoutWritingThenExecutesOnlyTheRequestedSafeOperation(string operation, string command, string flag)
    {
        var fixture = new Fixture(); var request = Request(operation); var plan = await fixture.Branches.PrepareAsync(Root, request);
        fixture.Writer.VerifyNoOtherCalls(); Assert.Equal(Sha, plan.Sha); Assert.Equal(64, plan.Token.Length);
        var result = await fixture.Branches.ExecuteAsync(Root, new(request, plan.Token));
        Assert.Equal(operation is "create" or "rename" ? "feature/new" : "feature/existing", result);
        fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(args => args[0] == command && args.Contains(flag) && args.Contains("--") && !args.Contains("--force") && !args.Contains("-D")), It.IsAny<CancellationToken>()), Times.Once);
        if (operation == "create") fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(args => args.Last() == Sha), It.IsAny<CancellationToken>()));
        if (operation == "checkout") fixture.Writer.Verify(x => x.RunAsync(Root, It.Is<IReadOnlyList<string>>(args => args.Contains("--no-overwrite-ignore") && args.Contains("--no-recurse-submodules")), It.IsAny<CancellationToken>()));
    }
    [Fact]
    public async Task RenamesCurrentBranchButNeverDeletesItAndRejectsBranchesUsedInOtherWorktrees()
    {
        var fixture = new Fixture(); await fixture.Branches.PrepareAsync(Root, Request("rename", "main"));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request("delete", "main")));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request("checkout", "main")));
        fixture.Read("for-each-ref", Refs("other-worktree"));
        foreach (var operation in new[] { "rename", "delete", "checkout" })
            await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request(operation)));
        fixture.Read("for-each-ref", Refs()); fixture.Read("status", "? untracked\0");
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request("checkout")));
        fixture.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task RefusesUnmergedBranchesInvalidOrDuplicateNamesMissingSourcesAndInProgressOperations()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request() with { NewName = "MAIN" }));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request(name: "missing")));
        fixture.Read("check-ref-format", "", 1); await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Branches.PrepareAsync(Root, Request()));
        fixture.Read("check-ref-format", ""); fixture.Read("merge-base", "", 1);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request("delete")));
        fixture.Status(commits: false); await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request("delete")));
        fixture.Status(); var state = Directory.CreateTempSubdirectory("GitCosmos-branch-state-");
        try
        {
            File.WriteAllText(Path.Combine(state.FullName, "MERGE_HEAD"), Sha); fixture.Read("--absolute-git-dir", state.FullName);
            await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.PrepareAsync(Root, Request()));
        }
        finally { File.Delete(Path.Combine(state.FullName, "MERGE_HEAD")); state.Delete(); }
    }
    [Fact]
    public async Task RejectsStaleTokensAfterRefIndexStatusRequestOrHeadChangesWithoutWriting()
    {
        foreach (var changed in new[] { "refs", "index", "status", "request", "head" })
        {
            var fixture = new Fixture(); var request = Request(); var plan = await fixture.Branches.PrepareAsync(Root, request);
            switch (changed)
            {
                case "refs": fixture.Read("for-each-ref", Refs(sha: OtherSha)); break;
                case "index": fixture.Read("ls-files", "changed index"); break;
                case "status": fixture.Read("status", "? file\0"); break;
                case "head": fixture.Read("--verify", OtherSha); break;
                default: request = request with { NewName = "other" }; break;
            }
            await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Branches.ExecuteAsync(Root, new(request, plan.Token)));
            fixture.Writer.VerifyNoOtherCalls();
        }
    }
    [Theory]
    [InlineData("for-each-ref", 2, false)]
    [InlineData("for-each-ref", 0, true)]
    [InlineData("check-ref-format", 2, false)]
    [InlineData("check-ref-format", 0, true)]
    [InlineData("merge-base", 2, false)]
    [InlineData("merge-base", 0, true)]
    public async Task CommandErrorsAndTruncationAreLocalizedAndNeverExecuteMutations(string flag, int exit, bool truncated)
    {
        var fixture = new Fixture(); fixture.Read(flag, "private details", exit, truncated);
        var error = await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Branches.PrepareAsync(Root, Request(flag == "merge-base" ? "delete" : "create")));
        Assert.DoesNotContain("private", error.Message); fixture.Writer.VerifyNoOtherCalls();
    }
    [Theory]
    [InlineData("invalid")]
    [InlineData("refs/tags/main\0aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\0\0")]
    [InlineData("refs/heads/main\0not-sha\0\0")]
    public async Task MalformedBranchRecordsFailSafely(string output)
    {
        var fixture = new Fixture(); fixture.Read("for-each-ref", output);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Branches.ReadAsync(Root));
    }
    [Fact]
    public async Task MovedRootsCancellationAndWriteFailuresNeverRetryOrForce()
    {
        var fixture = new Fixture(); fixture.Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root + "-moved", Root));
        await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Branches.ReadAsync(Root));
        fixture = new Fixture(); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Branches.PrepareAsync(Root, Request(), source.Token));
        fixture = new Fixture(); var request = Request(); var plan = await fixture.Branches.PrepareAsync(Root, request);
        fixture.Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "private", false));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Branches.ExecuteAsync(Root, new(request, plan.Token)));
        fixture.Writer.Verify(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task BranchAndIndexOperationsShareTheSameCommonDirectoryMutationLock()
    {
        var fixture = new Fixture(); var request = Request(); var plan = await fixture.Branches.PrepareAsync(Root, request);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(async () => { entered.SetResult(); await release.Task; return new LimitedGitOutput(0, "", false); });
        var first = fixture.Branches.ExecuteAsync(Root, new(request, plan.Token)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new GitChangesSnapshot(new("main", false, true, 1, 1, 0, 0, 0, 0, 0, 0, 1), [new("file.txt", null, ".", "M", false, false, false)]));
        var editor = new RepositoryEditor(fixture.Reader.Object, fixture.Writer.Object, fixture.Inspector.Object, fixture.Changes.Object);
        var waiting = editor.PrepareAsync(Root, new(1, 0, "stage", ["file.txt"]));
        try { Assert.False(waiting.IsCompleted); }
        finally { release.TrySetResult(); }
        await first; Assert.Equal(1, (await waiting).FileCount);
    }
    [Fact]
    public async Task RealGitCreatesRenamesSwitchesAndDeletesOnlyMergedLocalBranchesAndPreservesFiles()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        // Keep exact content assertions independent of the machine's Git line-ending settings.
        await fixture.RunAsync(root, "config", "--local", "core.autocrlf", "false");
        await fixture.RunAsync(root, "config", "--local", "core.eol", "lf");
        await fixture.RunAsync(root, "config", "user.name", "Tests"); await fixture.RunAsync(root, "config", "user.email", "test@example.invalid");
        await fixture.RunAsync(root, "config", "commit.gpgSign", "false"); await fixture.RunAsync(root, "config", "core.hooksPath", Path.Combine(fixture.Root, "no-hooks"));
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(runner);
        var branches = new RepositoryBranches(runner, new GitMutationRunner(), inspector, new RepositoryChangesReader(runner, inspector));
        Assert.Empty((await branches.ReadAsync(root)).Branches);
        File.WriteAllText(Path.Combine(root, "file.txt"), "preserved\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "--quiet", "-m", "initial");
        var original = (await branches.ReadAsync(root)).CurrentBranch;
        async Task Apply(RepositoryBranchRequest request) { var plan = await branches.PrepareAsync(root, request); await branches.ExecuteAsync(root, new(request, plan.Token)); }
        await Apply(Request() with { Name = original, NewName = "feature/á-new" });
        Assert.Equal(original, (await branches.ReadAsync(root)).CurrentBranch);
        await Apply(Request("rename", "feature/á-new") with { NewName = "feature/renamed" });
        await Apply(Request("checkout", "feature/renamed")); Assert.Equal("feature/renamed", (await branches.ReadAsync(root)).CurrentBranch);
        File.WriteAllText(Path.Combine(root, "file.txt"), "feature\n");
        await Assert.ThrowsAsync<ConflictAppException>(() => branches.PrepareAsync(root, Request("checkout", original)));
        await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "--quiet", "-m", "feature change");
        await Apply(Request("checkout", original));
        await Assert.ThrowsAsync<ConflictAppException>(() => branches.PrepareAsync(root, Request("delete", "feature/renamed")));
        await fixture.RunAsync(root, "merge", "--ff-only", "feature/renamed"); await Apply(Request("delete", "feature/renamed"));
        Assert.DoesNotContain((await branches.ReadAsync(root)).Branches, branch => branch.Name == "feature/renamed");
        Assert.Equal("feature\n", File.ReadAllText(Path.Combine(root, "file.txt")));
        await Apply(Request("rename", original) with { NewName = "renamed-current" });
        Assert.Equal("renamed-current", (await branches.ReadAsync(root)).CurrentBranch);
        await Apply(Request() with { Name = "renamed-current", NewName = "protected-target" });
        await Apply(Request("checkout", "protected-target"));
        var ignored = Path.Combine(root, "protected.txt");
        Directory.CreateDirectory(Path.Combine(root, ".git", "info"));
        File.WriteAllText(Path.Combine(root, ".git", "info", "exclude"), "protected.txt\n");
        File.WriteAllText(ignored, "tracked target\n"); await fixture.RunAsync(root, "add", "-f", "--", "protected.txt"); await fixture.RunAsync(root, "commit", "--quiet", "-m", "tracked target");
        await Apply(Request("checkout", "renamed-current")); File.WriteAllText(ignored, "local ignored content\n");
        var protectedRequest = Request("checkout", "protected-target"); var protectedPlan = await branches.PrepareAsync(root, protectedRequest);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => branches.ExecuteAsync(root, new(protectedRequest, protectedPlan.Token)));
        Assert.Equal("local ignored content\n", File.ReadAllText(ignored));
    }
}
