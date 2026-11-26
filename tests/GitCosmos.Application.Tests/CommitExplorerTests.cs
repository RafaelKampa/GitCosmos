using GitCosmos.Application.DTOs.Requests.Repository;
using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;
namespace GitCosmos.Application.Tests;

public class CommitExplorerTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", Parent = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", Blob = "cccccccccccccccccccccccccccccccccccccccc";
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("GitCosmos-commit-explorer-").FullName;
        public Mock<IBoundedGitProcessRunner> Runner { get; } = new();
        public Mock<IGitRepositoryInspector> Inspector { get; } = new();
        public Mock<IRepositoryHistoryReader> History { get; } = new();
        public Mock<IGitBlobReader> Blobs { get; } = new();
        public Mock<ICommitFileDesktop> Desktop { get; } = new();
        public string Tree = $"100644 blob {Blob}\tfile.txt\0";
        public string ParentTree = $"100644 blob {Blob}\tfile.txt\0";
        public string Output = "";
        public GitCommitDetails Details = new(new(Sha, [Parent], "Author", "mail", "2026-11-24T00:00:00Z", "Subject", "", "Body"), [new("file.txt", null, "M")]);
        public Func<IReadOnlyList<string>, LimitedGitOutput?>? Override;
        public CommitExplorer Explorer { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, ".git"));
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", Root, Root));
            Runner.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, IReadOnlyList<string> args, int _, CancellationToken _) =>
                Override?.Invoke(args) ?? new LimitedGitOutput(0, args[0] == "ls-tree" ? (args[^1] == Parent ? ParentTree : Tree) : args[0] == "rev-parse" ? (args.Contains("--git-common-dir") || args.Contains("--absolute-git-dir") ? Path.Combine(Root, ".git") : args[^1].StartsWith(Parent) ? Parent : Sha) : Output, false));
            Blobs.Setup(x => x.ReadAsync(Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes("<script>literal</script>\n"));
            History.Setup(x => x.ReadCommitAsync(Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Details);
            Explorer = new(Runner.Object, Inspector.Object, History.Object, Blobs.Object, Desktop.Object);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public async Task ReadsCompleteImmutableTreeAndUtf8BinaryAndSubmoduleContent()
    {
        using var f = new Fixture();
        Assert.Equal("file.txt", Assert.Single(await f.Explorer.TreeAsync(f.Root, Sha, default)).Path);
        var content = await f.Explorer.FileAsync(f.Root, Sha, "file.txt", default);
        Assert.Equal("text", content.Kind); Assert.Contains("<script>", content.Text); Assert.Equal(Path.Combine(f.Root, "file.txt"), content.FullPath);
        foreach (var bytes in new[] { new byte[] { 1, 0, 2 }, new byte[] { 255, 254 } })
        {
            f.Blobs.Setup(x => x.ReadAsync(f.Root, Blob, It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
            content = await f.Explorer.FileAsync(f.Root, Sha, "file.txt", default); Assert.Equal("binary", content.Kind); Assert.Empty(content.Text); Assert.Equal(bytes, Convert.FromBase64String(content.Base64));
        }
        f.Tree = $"160000 commit {Blob}\tmodule\0";
        Assert.Equal("submodule", (await f.Explorer.FileAsync(f.Root, Sha, "module", default)).Kind);
        await Assert.ThrowsAsync<NotFoundAppException>(() => f.Explorer.FileAsync(f.Root, Sha, "missing", default));
    }
    [Theory]
    [InlineData("malformed")]
    [InlineData("bad\tfile\0")]
    [InlineData("100644 blob nope\tfile\0")]
    [InlineData("000000 blob cccccccccccccccccccccccccccccccccccccccc\tfile\0")]
    public async Task RejectsMalformedTreeRecords(string tree)
    {
        using var f = new Fixture(); f.Tree = tree; await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
    }
    [Fact]
    public async Task RejectsInvalidCommitsRootsLimitsAndGitFailures()
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, "HEAD", default));
        f.Inspector.Setup(x => x.InspectAsync(f.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("other", Path.Combine(f.Root, "other"), ""));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
        f.Inspector.Setup(x => x.InspectAsync(f.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", f.Root, f.Root));
        f.Override = _ => new(0, Parent, false); await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
        f.Override = _ => new(1, "private stderr", false); await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
        f.Override = _ => new(0, "", true); await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
        f.Override = null; f.Tree = string.Concat(Enumerable.Range(0, 10001).Select(i => $"100644 blob {Blob}\tf{i}\0"));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.TreeAsync(f.Root, Sha, default));
    }
    [Fact]
    public async Task ReadsRenameFollowingFileHistoryAndLineAttribution()
    {
        using var f = new Fixture();
        f.Output = $"{Sha}\0{Parent}\0Author\0mail\02026-11-24T00:00:00Z\0subject\0\0body\0";
        Assert.Single(await f.Explorer.HistoryAsync(f.Root, Sha, "file.txt", default));
        f.Output = $"{Sha} 1 1 1\nauthor Author Name\nauthor-time 1234\nfilename file.txt\n\tliteral text\n";
        var line = Assert.Single(await f.Explorer.BlameAsync(f.Root, Sha, "file.txt", default));
        Assert.Equal("Author Name", line.Author); Assert.Equal("literal text", line.Text); Assert.Equal("1234", line.Time); Assert.Equal(1, line.Line);
        f.Blobs.Setup(x => x.ReadAsync(f.Root, Blob, It.IsAny<CancellationToken>())).ReturnsAsync(new byte[] { 0 });
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.BlameAsync(f.Root, Sha, "file.txt", default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => f.Explorer.HistoryAsync(f.Root, Sha, "unlisted", default));
        f.Tree = ""; f.Output = ""; Assert.Empty(await f.Explorer.HistoryAsync(f.Root, Sha, "file.txt", default));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoresOnlyReviewedFileAndRejectsChangedWorkingContent(bool before)
    {
        using var f = new Fixture(); var full = Path.Combine(f.Root, "file.txt"); await File.WriteAllTextAsync(full, "local");
        var request = new CommitRestoreRequest(new(1, 0, Sha, "file.txt"), before);
        var plan = await f.Explorer.PrepareAsync(f.Root, request, default); Assert.Equal(before ? Parent : Sha, plan.SourceSha); Assert.True(plan.Exists);
        await File.WriteAllTextAsync(full, "changed");
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Explorer.RestoreAsync(f.Root, request with { Token = plan.Token }, default));
        plan = await f.Explorer.PrepareAsync(f.Root, request, default);
        await f.Explorer.RestoreAsync(f.Root, request with { Token = plan.Token }, default);
        Assert.Equal("<script>literal</script>\n", await File.ReadAllTextAsync(full));
        Assert.Empty(Directory.GetFiles(f.Root, ".gitcosmos-restore-*"));
        plan = await f.Explorer.PrepareAsync(f.Root, request, default);
        f.Blobs.Setup(x => x.ReadAsync(f.Root, Blob, It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes("source changed"));
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Explorer.RestoreAsync(f.Root, request with { Token = plan.Token }, default));
    }
    [Fact]
    public async Task HandlesAbsentVersionsRenamesAndRestorationGuards()
    {
        using var f = new Fixture(); var request = new CommitRestoreRequest(new(1, 0, Sha, "file.txt"), true);
        f.Details = f.Details with { Files = [new("file.txt", "old.txt", "R100")] }; f.ParentTree = $"100644 blob {Blob}\told.txt\0";
        Assert.Equal("old.txt", (await f.Explorer.PrepareAsync(f.Root, request, default)).SourcePath);
        f.ParentTree = ""; var full = Path.Combine(f.Root, "file.txt"); await File.WriteAllTextAsync(full, "remove explicitly");
        var plan = await f.Explorer.PrepareAsync(f.Root, request, default); Assert.False(plan.Exists);
        await f.Explorer.RestoreAsync(f.Root, request with { Token = plan.Token }, default); Assert.False(File.Exists(full));
        f.Details = f.Details with { Commit = f.Details.Commit with { Parents = [] } };
        Assert.Empty((await f.Explorer.PrepareAsync(f.Root, request, default)).SourceSha);
        f.Details = f.Details with { Commit = f.Details.Commit with { Parents = [Parent] } }; f.ParentTree = $"120000 blob {Blob}\told.txt\0";
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.PrepareAsync(f.Root, request, default));
        f.ParentTree = ""; Directory.CreateDirectory(full); await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.PrepareAsync(f.Root, request, default)); Directory.Delete(full);
        await File.WriteAllTextAsync(Path.Combine(f.Root, ".git", "MERGE_HEAD"), Sha);
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Explorer.PrepareAsync(f.Root, request, default));
        File.Delete(Path.Combine(f.Root, ".git", "MERGE_HEAD"));
        await File.WriteAllBytesAsync(full, new byte[8_000_001]);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.PrepareAsync(f.Root, request, default));
        await Assert.ThrowsAsync<NotFoundAppException>(() => f.Explorer.PrepareAsync(f.Root, request with { File = request.File with { Path = "missing" } }, default));
    }
    [Theory]
    [InlineData("../outside")]
    [InlineData(".git/config")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("/absolute")]
    [InlineData("folder//name")]
    public async Task RejectsFilesystemEscapesBeforeNativeOrRestoreOperations(string path)
    {
        using var f = new Fixture(); f.Tree = $"100644 blob {Blob}\t{path}\0";
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.FileAsync(f.Root, Sha, path, default));
    }
    [Fact]
    public async Task DesktopActionsUseExportedSnapshotBytesAndNeverUnreviewedShellCommands()
    {
        using var f = new Fixture(); var file = new CommitFileSelection(1, 0, Sha, "file.txt");
        f.Desktop.Setup(x => x.Tools()).Returns([new("vscode", "VS Code")]); Assert.Single(f.Explorer.Tools());
        await f.Explorer.DesktopAsync(f.Root, new(file, "open"), default); f.Desktop.Verify(x => x.Open("file.txt", It.IsAny<byte[]>()), Times.Once);
        await f.Explorer.DesktopAsync(f.Root, new(file, "reveal"), default); f.Desktop.Verify(x => x.Reveal(Path.Combine(f.Root, "file.txt")), Times.Once);
        await f.Explorer.DesktopAsync(f.Root, new(file, "diff", "vscode"), default); f.Desktop.Verify(x => x.Diff("vscode", "file.txt", It.IsAny<byte[]>(), It.IsAny<byte[]>()), Times.Once);
        f.Tree = ""; f.ParentTree = ""; f.Details = f.Details with { Commit = f.Details.Commit with { Parents = [] } };
        await f.Explorer.DesktopAsync(f.Root, new(file, "diff", "vscode"), default);
        await Assert.ThrowsAsync<NotFoundAppException>(() => f.Explorer.DesktopAsync(f.Root, new(file, "open"), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.DesktopAsync(f.Root, new(file, "invalid"), default));
        f.Tree = $"160000 commit {Blob}\tfile.txt\0";
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Explorer.DesktopAsync(f.Root, new(file, "open"), default));
    }
}
