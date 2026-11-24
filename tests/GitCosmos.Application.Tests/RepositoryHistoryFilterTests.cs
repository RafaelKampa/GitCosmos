using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryHistoryFilterTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Other = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "GitCosmos-filter-history");
    private static string Record(string sha, string subject = "Subject", string message = "Body") => $"{sha}\0\0Author\0test@example.invalid\02026-11-22T00:00:00Z\0{subject}\0\0{message}\0";
    private sealed class Fixture
    {
        public Mock<IBoundedGitProcessRunner> Runner { get; } = new();
        public RepositoryHistoryReader Reader { get; }
        public Fixture()
        {
            var inspector = new Mock<IGitRepositoryInspector>(); inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("Repo", Root, Root));
            Runner.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "", false));
            Read("refs/heads/main^{commit}", Sha);
            Reader = new(Runner.Object, inspector.Object);
        }
        public void Read(string flag, string output, int exit = 0, bool truncated = false) => Runner.Setup(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains(flag)), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(exit, output, truncated));
    }
    [Fact]
    public async Task BranchFilterUsesOnlyTheResolvedLocalCommitAndBindsPaginationToFilters()
    {
        var fixture = new Fixture(); fixture.Read("log", Record(Sha) + Record(Other));
        var page = await fixture.Reader.ReadFilteredAsync(Root, 0, 1, null, "main", null);
        Assert.Equal(Sha, Assert.Single(page.Commits).Sha); Assert.True(page.HasMore);
        fixture.Runner.Verify(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("log") && a.Contains(Sha) && !a.Contains("--all") && a.Last() == "--"), 2_000_000, It.IsAny<CancellationToken>()));
        await fixture.Reader.ReadFilteredAsync(Root, 1, 1, page.Snapshot, "main", null);
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Reader.ReadFilteredAsync(Root, 1, 1, page.Snapshot, "other", null));
        await Assert.ThrowsAsync<ConflictAppException>(() => fixture.Reader.ReadFilteredAsync(Root, 1, 1, page.Snapshot, "main", "changed"));
    }
    [Fact]
    public async Task SearchIsLiteralCaseInsensitiveAcrossSubjectsBodiesAndShaPrefixesAndPaginatesMatches()
    {
        var fixture = new Fixture(); fixture.Read("log", Record(Sha, "[Literal] title") + Record(Other, "other", "body [literal]") + Record(new string('c', 40), "unrelated"));
        var first = await fixture.Reader.ReadFilteredAsync(Root, 0, 1, null, null, " [LITERAL] ");
        Assert.Equal(Sha, Assert.Single(first.Commits).Sha); Assert.True(first.HasMore);
        var second = await fixture.Reader.ReadFilteredAsync(Root, 1, 1, first.Snapshot, null, "[LITERAL]");
        Assert.Equal(Other, Assert.Single(second.Commits).Sha); Assert.False(second.HasMore);
        var sha = await fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, "", "BBBBBBB"); Assert.Equal(Other, Assert.Single(sha.Commits).Sha);
        Assert.Empty((await fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, null, "absent")).Commits);
    }
    [Fact]
    public async Task SearchesBeyondTheFirstBatchAndReportsTheBoundInsteadOfIncompleteResults()
    {
        var fixture = new Fixture(); var nonmatching = string.Concat(Enumerable.Repeat(Record(Sha), 200));
        fixture.Read("log", nonmatching);
        fixture.Read("--skip=200", Record(Other, "Needle"));
        Assert.Equal(Other, Assert.Single((await fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, null, "needle")).Commits).Sha);
        fixture = new Fixture(); fixture.Read("log", nonmatching);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, null, "needle"));
    }
    [Theory]
    [InlineData("check-ref-format", 1, false, "")]
    [InlineData("check-ref-format", 0, true, "")]
    [InlineData("refs/heads/main^{commit}", 1, false, "")]
    [InlineData("refs/heads/main^{commit}", 0, true, "")]
    [InlineData("refs/heads/main^{commit}", 0, false, "bad")]
    public async Task InvalidOrMissingBranchesNeverBecomeRevisionExpressions(string flag, int exit, bool truncated, string output)
    {
        var fixture = new Fixture(); fixture.Read(flag, output, exit, truncated);
        if (flag == "check-ref-format") await Assert.ThrowsAsync<ValidationAppException>(() => fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, "main", null));
        else await Assert.ThrowsAsync<NotFoundAppException>(() => fixture.Reader.ReadFilteredAsync(Root, 0, 50, null, "main", null));
        fixture.Runner.Verify(x => x.RunLimitedAsync(Root, It.Is<IReadOnlyList<string>>(a => a.Contains("log")), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task RealGitBranchSearchFindsOnlyReachableMatchesWithoutCheckoutOrIndexChanges()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-qm", "Shared root");
        var original = (await fixture.RunAsync(root, "branch", "--show-current")).Trim();
        await fixture.RunAsync(root, "branch", "feature/test");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "--allow-empty", "-qm", "Only original [literal]");
        var tip = (await fixture.RunAsync(root, "rev-parse", "HEAD")).Trim();
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var reader = new RepositoryHistoryReader(runner, new GitRepositoryInspector(runner));
        Assert.Equal(tip, Assert.Single((await reader.ReadFilteredAsync(root, 0, 50, null, original, "[LITERAL]")).Commits).Sha);
        Assert.Empty((await reader.ReadFilteredAsync(root, 0, 50, null, "feature/test", "[literal]")).Commits);
        Assert.Equal(tip, Assert.Single((await reader.ReadFilteredAsync(root, 0, 50, null, original, tip[..8])).Commits).Sha);
        Assert.Equal(original, (await fixture.RunAsync(root, "branch", "--show-current")).Trim());
        Assert.Equal("", await fixture.RunAsync(root, "status", "--porcelain"));
    }
}
