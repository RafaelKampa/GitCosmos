using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryHistoryTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Parent = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Root = "C:\\history-fixture";
    private static string Record(string sha = Sha, string parents = "") =>
        $"{sha}\0{parents}\0Áuthor\0test@example.invalid\02026-11-22T00:00:00Z\0Subject\0tag: refs/tags/v1, refs/heads/main\0Body\nwith tabs\tand Unicode ç\0";

    [Fact]
    public void ParsersPreserveMetadataParentsAndExactFilePaths()
    {
        Assert.Empty(GitHistoryParser.ParseCommits("")); Assert.Empty(GitHistoryParser.ParseFiles(""));
        var commit = Assert.Single(GitHistoryParser.ParseCommits(Record(parents: Parent + " " + Sha)));
        Assert.Equal(new[] { Parent, Sha }, commit.Parents); Assert.Contains("\tand Unicode ç", commit.Message);
        Assert.Equal("Áuthor", commit.Author); Assert.Contains("refs/tags/v1", commit.Refs);
        var files = GitHistoryParser.ParseFiles("M\0file with\nnewline\0R100\0old ç\0new ç\0C100\0source\0copy\0");
        Assert.Equal("file with\nnewline", files[0].Path); Assert.Equal("old ç", files[1].OriginalPath); Assert.Equal("copy", files[2].Path);
        Assert.True(GitHistoryParser.IsSha(new string('c', 64))); Assert.False(GitHistoryParser.IsSha("HEAD"));
    }

    [Theory]
    [InlineData("missing terminator")]
    [InlineData("wrong\0")]
    public void CommitParserRejectsMalformedRecords(string output) => Assert.Throws<OperationFailedAppException>(() => GitHistoryParser.ParseCommits(output));

    [Fact]
    public void CommitParserRejectsBadIdentifiersParentsAndDates()
    {
        foreach (var output in new[] { Record("bad"), Record(parents: "bad"), Record().Replace("2026-11-22T00:00:00Z", "bad") })
            Assert.Throws<OperationFailedAppException>(() => GitHistoryParser.ParseCommits(output));
    }

    [Theory]
    [InlineData("M\0file")]
    [InlineData("\0file\0")]
    [InlineData("?\0file\0")]
    [InlineData("M\0")]
    [InlineData("R100\0old\0")]
    [InlineData("M\0\0")]
    public void FileParserRejectsIncompleteRecords(string output) => Assert.Throws<OperationFailedAppException>(() => GitHistoryParser.ParseFiles(output));

    private static (RepositoryHistoryReader Reader, Mock<IBoundedGitProcessRunner> Runner, Mock<IGitRepositoryInspector> Inspector) Fake()
    {
        var runner = new Mock<IBoundedGitProcessRunner>(); var inspector = new Mock<IGitRepositoryInspector>();
        inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root, Root));
        runner.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "", false));
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("show")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, Record(parents: Parent), false));
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--name-status")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "R100\0old\0file\0", false));
        return (new(runner.Object, inspector.Object), runner, inspector);
    }

    [Fact]
    public async Task PaginationKeepsReferenceSnapshotAndRejectsChangedReferences()
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("log")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, Record() + Record(Parent), false));
        var first = await reader.ReadAsync(Root, 0, 1, null);
        Assert.True(first.HasMore); Assert.Single(first.Commits);
        await reader.ReadAsync(Root, 1, 1, first.Snapshot);
        runner.Verify(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--skip=1") && a.Contains("--max-count=2") && a.Contains("--single-worktree")), 2_000_000, It.IsAny<CancellationToken>()));
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadAsync(Root, 1, 1, new string('0', 64)));
        runner.SetupSequence(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("for-each-ref")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "before", false)).ReturnsAsync(new LimitedGitOutput(0, "after", false));
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadAsync(Root, 0, 1, null));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public async Task MetadataFailuresAreBoundedAndLocalized(int exit, bool truncated)
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("for-each-ref")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(exit, "", truncated));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadAsync(Root, 0, 1, null));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(0, true)]
    public async Task HeadFailuresAreNotTreatedAsEmptyHistory(int exit, bool truncated)
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("rev-parse")), 256, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(exit, "", truncated));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadAsync(Root, 0, 1, null));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(0, true)]
    public async Task SymbolicHeadFailuresAreNotIgnored(int exit, bool truncated)
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("symbolic-ref")), 32768, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(exit, "", truncated));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadAsync(Root, 0, 1, null));
    }

    [Fact]
    public async Task ChangingTheActiveBranchAtTheSameCommitInvalidatesPagination()
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("symbolic-ref")), 32768, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "refs/heads/main", false));
        var first = await reader.ReadAsync(Root, 0, 1, null);
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("symbolic-ref")), 32768, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "refs/heads/other", false));
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadAsync(Root, 1, 1, first.Snapshot));
    }

    [Fact]
    public async Task RevalidatesRootCancellationAndCommitIdentity()
    {
        var (reader, runner, inspector) = Fake();
        inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root + "-moved", Root));
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadAsync(Root, 0, 1, null));
        inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root, Root));
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadCommitAsync(Root, "HEAD"));
        await Assert.ThrowsAsync<NotFoundAppException>(() => reader.ReadDiffAsync(Root, Sha, "not-listed"));
        foreach (var result in new[] { new LimitedGitOutput(1, "", false), new(0, "", false), new(0, Record(Parent), false) })
        {
            runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("show")), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);
            await Assert.ThrowsAsync<NotFoundAppException>(() => reader.ReadCommitAsync(Root, Sha));
        }
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("show")), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, "", true));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadCommitAsync(Root, Sha));
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadCommitAsync(Root, Sha, source.Token));
    }

    [Theory]
    [InlineData("binary", "-\t-\tfile\0", "", false, false)]
    [InlineData("text", "1\t1\tfile\0", "+text", false, false)]
    [InlineData("empty", "", "", false, false)]
    [InlineData("tooLarge", "", "partial", true, false)]
    [InlineData("tooLarge", "partial", "", false, true)]
    public async Task CommitDiffUsesLiteralImmutablePathsAndOutputLimits(string kind, string numbers, string patch, bool truncated, bool numbersTruncated)
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--numstat")), 200_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, numbers, numbersTruncated));
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--patch")), 200_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, patch, truncated));
        Assert.Equal(kind, (await reader.ReadDiffAsync(Root, Sha, "file")).Kind);
        runner.Verify(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--numstat") && a.Contains("--literal-pathspecs") && a.Contains("--no-ext-diff") && a.Contains("--no-textconv") && a.Contains(Parent) && a.Contains(Sha) && a[a.Count - 2] == "file" && a[a.Count - 1] == "old"), 200_000, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("--numstat")]
    [InlineData("--patch")]
    public async Task DiffFailuresAreNotPresentedAsEmptyContent(string flag)
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains(flag)), 200_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(1, "", false));
        await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadDiffAsync(Root, Sha, "file"));
    }

    [Fact]
    public async Task ExcessiveDiffLinesAreNotShownAsComplete()
    {
        var (reader, runner, _) = Fake();
        runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("--patch")), 200_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, new string('\n', 4001), false));
        Assert.Equal("tooLarge", (await reader.ReadDiffAsync(Root, Sha, "file")).Kind);
    }

    [Fact]
    public async Task RealGitHistoryIncludesRootMergesTagsRemotesAndCommittedDiffsWithoutChangingIndex()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
        var reader = new RepositoryHistoryReader(runner, new GitRepositoryInspector(runner));
        Assert.Empty((await reader.ReadAsync(root, 0, 50, null)).Commits);
        File.WriteAllText(Path.Combine(root, "old ç.txt"), "initial\n");
        File.WriteAllBytes(Path.Combine(root, "binary.dat"), [0, 1, 2]);
        await fixture.RunAsync(root, "add", "--all");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "-qm", "Root");
        var rootSha = (await runner.RunAsync(root, ["rev-parse", "HEAD"])).Output.Trim();
        Assert.Empty((await reader.ReadCommitAsync(root, rootSha)).Commit.Parents);
        Assert.Contains("+initial", (await reader.ReadDiffAsync(root, rootSha, "old ç.txt")).Patch);
        Assert.Equal("binary", (await reader.ReadDiffAsync(root, rootSha, "binary.dat")).Kind);
        await fixture.RunAsync(root, "branch", "feature");
        await fixture.RunAsync(root, "mv", "old ç.txt", "[new] ç.txt");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "-qm", "Rename");
        var renameSha = (await runner.RunAsync(root, ["rev-parse", "HEAD"])).Output.Trim();
        Assert.Equal("old ç.txt", Assert.Single((await reader.ReadCommitAsync(root, renameSha)).Files).OriginalPath);
        Assert.Contains("rename from", (await reader.ReadDiffAsync(root, renameSha, "[new] ç.txt")).Patch);
        await fixture.RunAsync(root, "checkout", "--quiet", "feature");
        File.WriteAllText(Path.Combine(root, "feature.txt"), "feature\n");
        await fixture.RunAsync(root, "add", "--all");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "-qm", "Feature");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "merge", "--no-ff", "-m", "Merge", renameSha);
        await fixture.RunAsync(root, "tag", "v1");
        await fixture.RunAsync(root, "update-ref", "refs/remotes/origin/feature", "HEAD");
        var index = File.ReadAllBytes(Path.Combine(root, ".git", "index"));
        var first = await reader.ReadAsync(root, 0, 2, null); Assert.True(first.HasMore);
        Assert.Contains("refs/tags/v1", first.Commits[0].Refs); Assert.Contains("refs/remotes/origin/feature", first.Commits[0].Refs);
        var second = await reader.ReadAsync(root, 1, 2, first.Snapshot); Assert.False(second.HasMore);
        Assert.Equal(4, first.Commits.Concat(second.Commits).Select(x => x.Sha).Distinct().Count());
        var merge = await reader.ReadCommitAsync(root, first.Commits[0].Sha); Assert.Equal(2, merge.Commit.Parents.Count);
        Assert.Contains(merge.Files, x => x.Path == "[new] ç.txt");
        Assert.Equal(index, File.ReadAllBytes(Path.Combine(root, ".git", "index")));
        await fixture.RunAsync(root, "tag", "v2");
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadAsync(root, 1, 2, first.Snapshot));
        await fixture.RunAsync(root, "checkout", "--detach", rootSha);
        Assert.NotEmpty((await reader.ReadAsync(root, 0, 50, null)).Commits);
    }
}
