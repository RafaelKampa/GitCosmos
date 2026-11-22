using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryStatusTests
{
    private const string Headers = "# branch.oid abc123\0# branch.head main\0";
    private static string Ordinary(string xy, string path = "file") => $"1 {xy} N... 100644 100644 100644 abc abc {path}\0";

    [Fact]
    public void ParserCountsDistinctPathsAndOverlappingIndexWorktreeChanges()
    {
        var output = Headers + "# future.header value\0! ignored\0" + Ordinary("MM") + Ordinary("A.") + Ordinary(".D")
            + Ordinary("T.") + "? nested/new file ç.txt\0"
            + "2 R. N... 100644 100644 100644 abc abc R100 target\0? original is not an untracked record\0"
            + "2 C. N... 100644 100644 100644 abc abc C100 copy\0source\0"
            + "u UU N... 100644 100644 100644 100644 abc abc abc conflict\0";
        var status = GitStatusParser.Parse(output);
        Assert.Equal("main", status.Branch); Assert.True(status.HasCommits); Assert.False(status.IsDetached);
        Assert.Equal(8, status.ChangedFiles); Assert.Equal(2, status.Modified); Assert.Equal(2, status.Added);
        Assert.Equal(1, status.Deleted); Assert.Equal(1, status.Renamed); Assert.Equal(1, status.Untracked);
        Assert.Equal(1, status.Conflicted); Assert.Equal(5, status.Staged); Assert.Equal(2, status.Unstaged);
    }

    [Theory]
    [InlineData("(initial)", "main", false, false)]
    [InlineData("abc", "(detached)", true, true)]
    public void ParserHandlesEmptyAndDetachedRepositories(string oid, string branch, bool commits, bool detached)
    {
        var status = GitStatusParser.Parse($"# branch.oid {oid}\0# branch.head {branch}\0");
        Assert.Equal(0, status.ChangedFiles); Assert.Equal(commits, status.HasCommits); Assert.Equal(detached, status.IsDetached);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# branch.oid abc\0")]
    [InlineData("# branch.head main\0")]
    [InlineData("# branch.head \0# branch.oid abc\0")]
    [InlineData("# branch.head main\0# branch.oid abc\0x")]
    [InlineData("# branch.head main\0# branch.oid abc\0x MM bad")]
    [InlineData("# branch.head main\0# branch.oid abc\01XMM data")]
    [InlineData("# branch.head main\0# branch.oid abc\01 MMXdata")]
    [InlineData("# branch.head main\0# branch.oid abc\02 R. data")]
    [InlineData("# branch.head main\0# branch.oid abc\02 R. data\0")]
    public void ParserRejectsIncompleteOutput(string output) => Assert.Throws<OperationFailedAppException>(() => GitStatusParser.Parse(output));

    [Fact]
    public void ParserDoesNotTreatFilenameNewlinesAsRecords()
    {
        var status = GitStatusParser.Parse(Headers + "? line\n# branch.head other\0" + Ordinary(".M", "unicode ç name"));
        Assert.Equal("main", status.Branch); Assert.Equal(1, status.Untracked); Assert.Equal(1, status.Modified);
    }

    [Fact]
    public async Task RealGitReportsUnbornCleanModifiedStagedDeletedAndRenamedStatesWithoutUpdatingTheIndex()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
        var reader = new RepositoryStatusReader(runner, new GitRepositoryInspector(runner));
        var unborn = await reader.ReadAsync(fixture.RepositoryPath);
        Assert.False(unborn.HasCommits); Assert.Equal(0, unborn.ChangedFiles);
        foreach (var name in new[] { "modify.txt", "delete.txt", "rename.txt" })
            File.WriteAllText(Path.Combine(fixture.RepositoryPath, name), "original\n");
        await fixture.RunAsync(fixture.RepositoryPath, "add", "--all");
        await fixture.RunAsync(fixture.RepositoryPath, "-c", "user.name=GitCosmos tests", "-c", "user.email=test@example.invalid", "commit", "--quiet", "-m", "fixture");
        Assert.Equal(0, (await reader.ReadAsync(fixture.RepositoryPath)).ChangedFiles);
        File.AppendAllText(Path.Combine(fixture.RepositoryPath, "modify.txt"), "changed\n");
        File.Delete(Path.Combine(fixture.RepositoryPath, "delete.txt"));
        await fixture.RunAsync(fixture.RepositoryPath, "mv", "rename.txt", "renamed.txt");
        File.WriteAllText(Path.Combine(fixture.RepositoryPath, "added.txt"), "staged\n");
        await fixture.RunAsync(fixture.RepositoryPath, "add", "added.txt");
        Directory.CreateDirectory(Path.Combine(fixture.RepositoryPath, "new folder"));
        File.WriteAllText(Path.Combine(fixture.RepositoryPath, "new folder", "file ç.txt"), "untracked");
        var index = Path.Combine(fixture.RepositoryPath, ".git", "index");
        var before = File.ReadAllBytes(index);
        var status = await reader.ReadAsync(fixture.RepositoryPath);
        Assert.Equal(5, status.ChangedFiles); Assert.Equal(1, status.Modified); Assert.Equal(1, status.Added);
        Assert.Equal(1, status.Deleted); Assert.Equal(1, status.Renamed); Assert.Equal(1, status.Untracked);
        Assert.Equal(2, status.Staged); Assert.Equal(2, status.Unstaged); Assert.Equal(before, File.ReadAllBytes(index));
        await fixture.RunAsync(fixture.RepositoryPath, "checkout", "--detach", "--quiet");
        Assert.True((await reader.ReadAsync(fixture.RepositoryPath)).IsDetached);
    }

    [Fact]
    public async Task ReaderUsesSafeMachineOutputAndRejectsChangedRootOrGitFailure()
    {
        var path = Path.GetFullPath("repo");
        var inspector = new Mock<IGitRepositoryInspector>();
        var runner = new Mock<IGitProcessRunner>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        inspector.Setup(x => x.InspectAsync(path, source.Token)).ReturnsAsync(new LocalGitRepository("repo", path, path));
        runner.Setup(x => x.RunAsync(path, It.Is<IReadOnlyList<string>>(a => a.Contains("--porcelain=v2") && a.Contains("-z")
            && a.Contains("--untracked-files=all") && a.Contains("core.fsmonitor=false") && a.Contains("--renames")), source.Token))
            .ReturnsAsync(new GitCommandResult(0, Headers));
        var reader = new RepositoryStatusReader(runner.Object, inspector.Object);
        Assert.Equal(0, (await reader.ReadAsync(path, source.Token)).ChangedFiles);
        runner.Setup(x => x.RunAsync(path, It.IsAny<IReadOnlyList<string>>(), source.Token)).ReturnsAsync(new GitCommandResult(1, "private diagnostic"));
        var error = await Assert.ThrowsAsync<OperationFailedAppException>(() => reader.ReadAsync(path, source.Token));
        Assert.Equal(LocalizedText.Get("dashboard.status_failed"), error.Message);
        inspector.Setup(x => x.InspectAsync(path, source.Token)).ReturnsAsync(new LocalGitRepository("parent", Path.GetDirectoryName(path)!, "parent"));
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadAsync(path, source.Token));
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(path, source.Token));
    }
}
