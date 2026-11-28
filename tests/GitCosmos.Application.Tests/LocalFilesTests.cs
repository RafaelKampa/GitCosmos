using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;
namespace GitCosmos.Application.Tests;
public class LocalFilesTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Fixture : IDisposable
    {
        public string Root = Directory.CreateTempSubdirectory("GitCosmos-local-files-").FullName;
        public string Index = $"100644 {Sha} 0\ta.cs\0"; public string Head = Sha; public string Tree = $"100644 blob {Sha}\ta.cs\0";
        public List<GitFileChange> Files = [new("a.cs", null, "M", "M", false, false, false), new("nested/new.vue", null, ".", "?", true, false, false)];
        public GitWorkingTreeStatus Status = new("main", false, true, 2, 1, 0, 0, 0, 1, 0, 1, 2);
        public string Blame = $"{Sha} 1 1 1\nauthor Author\nauthor-time 1700000000\nfilename a.cs\n\told\n";
        public string Log = $"{Sha}\0\0Author\0author@example.invalid\02026-11-27T00:00:00Z\0Subject\0\0Subject\0";
        public byte[] Blob = Encoding.UTF8.GetBytes("old\n"); public bool Fail; public bool Truncated; public bool MutationFailed;
        public Mock<IBoundedGitProcessRunner> Reader = new(); public Mock<IGitMutationRunner> Writer = new(); public Mock<IGitRepositoryInspector> Inspector = new();
        public Mock<IGitBlobReader> Blobs = new(); public Mock<IRepositoryChangesReader> Changes = new(); public Mock<ICommitFileDesktop> Desktop = new(); public Mock<IWorkingFileEditors> Editors = new();
        public LocalFiles Service;
        public LocalFilesRequest Request(string action = "info", string[]? paths = null) => new(1, paths ?? ["a.cs"], "working", action);
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, ".git")); Directory.CreateDirectory(Path.Combine(Root, "nested")); File.WriteAllText(Path.Combine(Root, "a.cs"), "new\n"); File.WriteAllText(Path.Combine(Root, "nested", "new.vue"), "<template>new</template>\n");
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", Root, Root));
            Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(() => new GitChangesSnapshot(Status, Files));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, IReadOnlyList<string> args, int _, CancellationToken _) => new LimitedGitOutput(Fail ? 1 : 0, args.Contains("--git-common-dir") || args.Contains("--absolute-git-dir") ? Path.Combine(Root, ".git") : args.Contains("ls-files") ? Index : args.Contains("ls-tree") ? Tree : args.Contains("blame") ? Blame : args.Contains("log") ? Log : args.Contains("diff") ? "diff --git a/a.cs b/a.cs\n--- a/a.cs\n+++ b/a.cs\n@@ -1 +1 @@\n-old\n+new\n" : Head, Truncated));
            Blobs.Setup(x => x.ReadAsync(Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Blob);
            Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new LimitedGitOutput(MutationFailed ? 1 : 0, "", false));
            Desktop.Setup(x => x.Tools()).Returns([new("vscode", "VS Code")]); Editors.Setup(x => x.Tools(It.IsAny<string>())).Returns([new("rider", "Rider")]);
            Service = new(Reader.Object, Writer.Object, Inspector.Object, Changes.Object, Blobs.Object, Desktop.Object, Editors.Object);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public async Task ReadsInfoPatchesHistoryAndWorkingOrStagedBlame()
    {
        using var f = new Fixture(); var info = await f.Service.ReadAsync(f.Root, f.Request(), default); Assert.Equal(Path.Combine(f.Root, "a.cs"), info.FullPath); Assert.Single(info.Editors); Assert.Single(info.Tools);
        Assert.Contains("+new", (await f.Service.ReadAsync(f.Root, f.Request("patch"), default)).Patch);
        Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("history"), default)).History);
        Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("blame"), default)).Blame);
        Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("blame") with { Area = "staged" }, default)).Blame);
        Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("lineHistory"), default)).History);
        f.Blame = f.Blame.Replace(Sha, new string('0', 40)); Assert.Empty((await f.Service.ReadAsync(f.Root, f.Request("lineHistory"), default)).History);
        f.Tree = ""; Assert.Empty((await f.Service.ReadAsync(f.Root, f.Request("blame"), default)).Blame);
        f.Status = f.Status with { HasCommits = false }; Assert.Empty((await f.Service.ReadAsync(f.Root, f.Request("history"), default)).History);
        f.Status = f.Status with { HasCommits = true }; File.Delete(Path.Combine(f.Root, "a.cs")); f.Tree = $"100644 blob {Sha}\ta.cs\0"; Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("blame"), default)).Blame);
    }
    [Theory]
    [InlineData("open")] [InlineData("reveal")] [InlineData("line")] [InlineData("diff")]
    public async Task UsesValidatedLocalPathsAndInstalledToolsForDesktopActions(string action)
    {
        using var f = new Fixture(); await f.Service.DesktopAsync(f.Root, f.Request(action) with { Tool = "rider", Start = 2, End = 2 }, default);
        if (action == "open") f.Editors.Verify(x => x.OpenWith(Path.Combine(f.Root, "a.cs")), Times.Once);
        if (action == "line") f.Editors.Verify(x => x.RevealLine(Path.Combine(f.Root, "a.cs"), "rider", 2), Times.Once);
        if (action == "reveal") f.Desktop.Verify(x => x.Reveal(Path.Combine(f.Root, "a.cs")), Times.Once);
        if (action == "diff") { f.Desktop.Verify(x => x.Diff("rider", "a.cs", It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "old\n"), It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "new\n")), Times.Once); await f.Service.DesktopAsync(f.Root, f.Request(action) with { Area = "staged", Tool = "rider" }, default); }
    }
    [Theory]
    [InlineData("file", "/nested/new.vue")]
    [InlineData("extension", "*.vue")]
    [InlineData("folder", "/nested/")]
    public async Task ReviewsIgnoreRulesAndAppendsOnlyAfterConfirmingTheExactState(string pattern, string expected)
    {
        using var f = new Fixture(); var ignore = Path.Combine(f.Root, ".gitignore"); await File.WriteAllTextAsync(ignore, "bin/");
        var request = f.Request("ignore", ["nested/new.vue"]) with { Pattern = pattern }; var plan = await f.Service.PrepareAsync(f.Root, request, default); Assert.Equal(expected + "\n", plan.Preview); Assert.Equal("bin/", await File.ReadAllTextAsync(ignore));
        await f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default); Assert.Equal("bin/\n" + expected + "\n", await File.ReadAllTextAsync(ignore)); Assert.True(File.Exists(Path.Combine(f.Root, "nested", "new.vue"))); f.Writer.VerifyNoOtherCalls();
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default));
    }
    [Fact]
    public async Task DiscardsOnlyUnstagedPathsAndStashesOnlyTheReviewedSelection()
    {
        using var f = new Fixture(); var request = f.Request("discard", ["a.cs", "nested/new.vue"]); var plan = await f.Service.PrepareAsync(f.Root, request, default); Assert.True(File.Exists(Path.Combine(f.Root, "nested", "new.vue")));
        await f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default);
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(a => a.SequenceEqual(new[] { "--literal-pathspecs", "restore", "--worktree", "--", "a.cs" })), It.IsAny<CancellationToken>()), Times.Once); Assert.False(File.Exists(Path.Combine(f.Root, "nested", "new.vue")));
        request = f.Request("stash"); plan = await f.Service.PrepareAsync(f.Root, request, default); await f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default);
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(a => a.Contains("stash") && a.Contains("--include-untracked") && a[a.Count - 1] == "a.cs"), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task RejectsStaleUnsafeUnsupportedAndPendingOperationsWithoutAutomaticRetry()
    {
        using var f = new Fixture(); var request = f.Request("discard"); var plan = await f.Service.PrepareAsync(f.Root, request, default); await File.AppendAllTextAsync(Path.Combine(f.Root, "a.cs"), "changed");
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default)); f.Writer.VerifyNoOtherCalls();
        plan = await f.Service.PrepareAsync(f.Root, request, default); f.MutationFailed = true; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default)); Assert.Single(f.Writer.Invocations);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, request with { Area = "staged" }, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, f.Request("ignore"), default));
        f.Status = f.Status with { HasCommits = false }; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, f.Request("stash"), default)); f.Status = f.Status with { HasCommits = true };
        File.WriteAllText(Path.Combine(f.Root, ".git", "MERGE_HEAD"), Sha); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, request, default)); File.Delete(Path.Combine(f.Root, ".git", "MERGE_HEAD"));
        f.Status = f.Status with { Conflicted = 1 }; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, request, default)); f.Status = f.Status with { Conflicted = 0 };
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Service.ReadAsync(f.Root, f.Request() with { Paths = ["missing"] }, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request("discard"), default)); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.DesktopAsync(f.Root, f.Request("info"), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.DesktopAsync(f.Root, f.Request("open", ["a.cs", "nested/new.vue"]), default));
        var original = f.Files[0]; foreach (var file in new[] { original with { IsConflicted = true }, original with { IsSubmodule = true }, original with { OriginalPath = "old.cs" } }) { f.Files[0] = file; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.PrepareAsync(f.Root, request, default)); }
    }
    [Fact]
    public async Task BoundsContentAndMetadataAndRejectsMalformedOrNonregularIndexEntries()
    {
        using var f = new Fixture();
        foreach (var index in new[] { "bad", $"100644 {Sha}\ta.cs\0", $"120000 {Sha} 0\ta.cs\0" }) { f.Index = index; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request(), default)); }
        f.Index = $"100644 {Sha} 0\ta.cs\0"; await File.WriteAllBytesAsync(Path.Combine(f.Root, "a.cs"), new byte[2000001]); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request("patch"), default)); Assert.NotEmpty((await f.Service.ReadAsync(f.Root, f.Request(), default)).FullPath);
        File.Delete(Path.Combine(f.Root, "a.cs")); Directory.CreateDirectory(Path.Combine(f.Root, "a.cs")); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request(), default)); Directory.Delete(Path.Combine(f.Root, "a.cs"));
        f.Fail = true; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Service.ReadAsync(f.Root, f.Request(), default)); f.Fail = false; f.Truncated = true; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Service.ReadAsync(f.Root, f.Request(), default)); f.Truncated = false;
        f.Inspector.Setup(x => x.InspectAsync(f.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("other", f.Root + "other", "")); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request(), default));
    }
    [Fact]
    public async Task RealGitDiscardPreservesStagedContentAndSelectedStashPreservesOtherFiles()
    {
        await using var f = await GitTestDirectory.CreateAsync(); var root = f.RepositoryPath;
        foreach (var setting in new[] { ("user.name", "GitCosmos"), ("user.email", "test@example.invalid"), ("commit.gpgsign", "false"), ("core.autocrlf", "false"), ("core.hooksPath", Path.Combine(f.Root, "no-hooks")) }) await f.RunAsync(root, "config", setting.Item1, setting.Item2);
        await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "base\n"); await File.WriteAllTextAsync(Path.Combine(root, "b.cs"), "base\n"); await f.RunAsync(root, "add", "--all"); await f.RunAsync(root, "commit", "-qm", "base");
        await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "staged\n"); await f.RunAsync(root, "add", "--", "a.cs"); await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "unstaged\n"); await File.WriteAllTextAsync(Path.Combine(root, "b.cs"), "other\n");
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(runner); var service = new LocalFiles(runner, new GitMutationRunner(), inspector, new RepositoryChangesReader(runner, inspector), new GitBlobReader("git", TimeSpan.FromSeconds(10)), Mock.Of<ICommitFileDesktop>(), Mock.Of<IWorkingFileEditors>());
        var request = new LocalFilesRequest(1, ["a.cs"], "working", "discard"); var plan = await service.PrepareAsync(root, request, default); await service.ExecuteAsync(root, request with { Token = plan.Token }, default);
        Assert.Equal("staged\n", await File.ReadAllTextAsync(Path.Combine(root, "a.cs"))); Assert.Equal("staged\n", await f.RunAsync(root, "show", ":a.cs")); Assert.Equal("other\n", await File.ReadAllTextAsync(Path.Combine(root, "b.cs")));
        request = request with { Area = "staged", Action = "stash" }; plan = await service.PrepareAsync(root, request, default); await service.ExecuteAsync(root, request with { Token = plan.Token }, default);
        Assert.Equal("base\n", await File.ReadAllTextAsync(Path.Combine(root, "a.cs"))); Assert.Equal("other\n", await File.ReadAllTextAsync(Path.Combine(root, "b.cs"))); Assert.NotEmpty(await f.RunAsync(root, "stash", "list"));
    }
    [Fact]
    public async Task BoundsLineOriginsAndRejectsBinaryOrMalformedBlameInput()
    {
        using var f = new Fixture(); var request = f.Request("lineHistory") with { End = 17 };
        f.Blame = string.Concat(Enumerable.Range(1, 17).Select(id => $"{id.ToString("x40")} 1 {id} 1\nauthor A\nauthor-time 1700000000\nfilename a.cs\n\tline\n"));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, request, default));
        f.Blame = $"{Sha} nope 1\n"; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, request, default));
        f.Blame = $"{Sha} 1 nope\n"; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, request, default));
        f.Blame = $"{Sha} 1 1\nfilename \"a.cs\"\n\tline\n"; Assert.Single((await f.Service.ReadAsync(f.Root, f.Request("blame"), default)).Blame);
        f.Blame = $"{Sha} 1 1\nfilename \"invalid\n"; await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, request, default));
        foreach (var bytes in new[] { new byte[] { 0 }, new byte[] { 255 }, Encoding.UTF8.GetBytes(new string('x', 200001)) }) { await File.WriteAllBytesAsync(Path.Combine(f.Root, "a.cs"), bytes); await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.ReadAsync(f.Root, f.Request("blame"), default)); }
    }
    [Fact]
    public async Task RechecksNewFilesBeforeDeletingAndCreatesAnAbsentIgnoreFile()
    {
        using var f = new Fixture(); var request = f.Request("ignore", ["nested/new.vue"]); var plan = await f.Service.PrepareAsync(f.Root, request, default);
        await f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default); Assert.Equal("/nested/new.vue\n", await File.ReadAllTextAsync(Path.Combine(f.Root, ".gitignore")));
        request = f.Request("discard", ["a.cs", "nested/new.vue"]); plan = await f.Service.PrepareAsync(f.Root, request, default);
        f.Writer.Setup(x => x.RunAsync(f.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).Returns(async (string _, IReadOnlyList<string> _, CancellationToken ct) => { await File.WriteAllTextAsync(Path.Combine(f.Root, "nested", "new.vue"), "changed externally", ct); return new LimitedGitOutput(0, "", false); });
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Service.ExecuteAsync(f.Root, request with { Token = plan.Token }, default));
        Assert.Equal("changed externally", await File.ReadAllTextAsync(Path.Combine(f.Root, "nested", "new.vue"))); Assert.Single(f.Writer.Invocations);
    }
    [Fact]
    public async Task RealGitTracesOnlyCommittedSelectedLinesThroughBlameAndLogL()
    {
        await using var f = await GitTestDirectory.CreateAsync(); var root = f.RepositoryPath;
        foreach (var setting in new[] { ("user.name", "GitCosmos"), ("user.email", "test@example.invalid"), ("commit.gpgsign", "false"), ("core.autocrlf", "false"), ("core.hooksPath", Path.Combine(f.Root, "no-hooks")) }) await f.RunAsync(root, "config", setting.Item1, setting.Item2);
        await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "unchanged\nold\n"); await f.RunAsync(root, "add", "--all"); await f.RunAsync(root, "commit", "-qm", "initial lines");
        await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "unchanged\nnew\n"); var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(runner);
        var service = new LocalFiles(runner, new GitMutationRunner(), inspector, new RepositoryChangesReader(runner, inspector), new GitBlobReader("git", TimeSpan.FromSeconds(10)), Mock.Of<ICommitFileDesktop>(), Mock.Of<IWorkingFileEditors>());
        var request = new LocalFilesRequest(1, ["a.cs"], "working", "lineHistory", Start: 1, End: 1); var view = await service.ReadAsync(root, request, default); Assert.Equal("initial lines", Assert.Single(view.History).Subject);
        view = await service.ReadAsync(root, request with { Start = 2, End = 2 }, default); Assert.Empty(view.History); Assert.Contains(view.Blame, line => line.Line == 2 && line.Sha.All(ch => ch == '0'));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RealGitExportsNewBinaryAndEmptyFilesAsApplicablePatches(bool binary)
    {
        await using var f = await GitTestDirectory.CreateAsync(); var root = f.RepositoryPath; var bytes = binary ? new byte[] { 0, 255, 1, 0, 7, 8, 9 } : Array.Empty<byte>();
        await File.WriteAllBytesAsync(Path.Combine(root, "new.bin"), bytes); var reader = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(reader); var blobs = new GitBlobReader("git", TimeSpan.FromSeconds(10));
        var service = new LocalFiles(reader, new GitMutationRunner(), inspector, new RepositoryChangesReader(reader, inspector), blobs, Mock.Of<ICommitFileDesktop>(), Mock.Of<IWorkingFileEditors>());
        var view = await service.ReadAsync(root, new(1, ["new.bin"], "working", "patch"), default); Assert.Contains("new file mode", view.Patch); if (binary) Assert.Contains("GIT binary patch", view.Patch);
        var patch = Path.Combine(f.Root, "new.patch"); await File.WriteAllTextAsync(patch, view.Patch, new UTF8Encoding(false)); await f.RunAsync(root, "apply", "--cached", "--check", patch); await f.RunAsync(root, "apply", "--cached", patch);
        var oid = (await f.RunAsync(root, "rev-parse", ":new.bin")).Trim(); Assert.Equal(bytes, await blobs.ReadAsync(root, oid, default)); Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(root, "new.bin")));
    }
}
