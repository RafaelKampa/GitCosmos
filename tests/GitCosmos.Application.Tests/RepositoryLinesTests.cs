using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;
namespace GitCosmos.Application.Tests;
public class RepositoryLinesTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Patch = "diff --git a/file.txt b/file.txt\n--- a/file.txt\n+++ b/file.txt\n@@ -1 +1,2 @@\n-old\n+new\n+extra\n";
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("GitCosmos-lines-").FullName;
        public string Full => Path.Combine(Root, "file.txt");
        public string Index = $"100644 {Sha} 0\tfile.txt\0"; public string Diff = Patch; public string Head = Sha;
        public GitFileChange File = new("file.txt", null, "M", "M", false, false, false);
        public GitWorkingTreeStatus Status = new("main", false, true, 1, 1, 0, 0, 0, 0, 0, 1, 1);
        public byte[] Baseline = Encoding.UTF8.GetBytes("old\n"); public byte[]? Written;
        public int ExitCode; public bool Truncated; public bool Missing; public bool FailHash; public bool FailUpdate; public bool Race;
        public Mock<IBoundedGitProcessRunner> Reader { get; } = new();
        public Mock<IGitMutationRunner> Writer { get; } = new();
        public Mock<IGitRepositoryInspector> Inspector { get; } = new();
        public Mock<IGitBlobReader> Blobs { get; } = new();
        public Mock<IRepositoryChangesReader> Changes { get; } = new();
        public RepositoryLineEditor Editor { get; }
        public RepositoryLinesRequest Request(string area = "working") => new(1, "file.txt", area);
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, ".git")); System.IO.File.WriteAllText(Full, "new\nextra\n");
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", Root, Root));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, IReadOnlyList<string> args, int _, CancellationToken _) => new LimitedGitOutput(ExitCode, args.Contains("--git-common-dir") || args.Contains("--absolute-git-dir") ? Path.Combine(Root, ".git") : args.Contains("ls-files") ? Index : args.Contains("diff") ? Diff : Head, Truncated));
            Changes.Setup(x => x.ReadAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(() => new GitChangesSnapshot(Status, Missing ? [] : [File]));
            Blobs.Setup(x => x.ReadAsync(Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Baseline);
            Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, IReadOnlyList<string> args, CancellationToken _) =>
            {
                if (args[0] == "hash-object") { Written = System.IO.File.ReadAllBytes(args[^1]); if (Race) Head = new string('b', 40); return new(FailHash ? 1 : 0, Sha, false); }
                return new(FailUpdate ? 1 : 0, "", false);
            });
            Editor = new(Reader.Object, Blobs.Object, Writer.Object, Inspector.Object, Changes.Object);
        }
        public async Task<(PrepareLinesRequest Request, RepositoryLinesPlan Plan)> Prepare(string area = "working", int[]? lines = null)
        {
            var file = Request(area); var read = await Editor.ReadAsync(Root, file, default);
            var request = new PrepareLinesRequest(file, lines ?? [4, 5], read.Token);
            return (request, await Editor.PrepareAsync(Root, request, default));
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Theory]
    [InlineData("working", "new\n")]
    [InlineData("staged", "old\nextra\n")]
    public async Task WritesOnlyReviewedIndexContentAndPreservesEveryWorkingByte(string area, string expected)
    {
        using var f = new Fixture(); if (area == "staged") f.Baseline = Encoding.UTF8.GetBytes("new\nextra\n");
        var before = await System.IO.File.ReadAllBytesAsync(f.Full); var (request, plan) = await f.Prepare(area);
        Assert.Equal(expected, plan.Result); Assert.Equal(2, plan.LineCount); Assert.False(plan.RemovesEntry); f.Writer.VerifyNoOtherCalls();
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), f.Written); Assert.Equal(before, await System.IO.File.ReadAllBytesAsync(f.Full));
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "update-index", "--add", "--cacheinfo", "100644", Sha, "file.txt" })), It.IsAny<CancellationToken>()), Times.Once);
        var temp = f.Writer.Invocations[0].Arguments[1] as IReadOnlyList<string>; Assert.False(System.IO.File.Exists(temp![^1]));
    }
    [Fact]
    public async Task SupportsNewFilesUnbornHeadAndUnstagingACompleteAddition()
    {
        using var f = new Fixture(); f.Index = ""; f.Baseline = Encoding.UTF8.GetBytes("new\nextra\n"); f.Status = f.Status with { HasCommits = false }; f.File = f.File with { IsUntracked = true, IndexStatus = "?", WorkTreeStatus = "?" };
        var view = await f.Editor.ReadAsync(f.Root, f.Request(), default); Assert.True(view.CanSelect); Assert.Contains("new file mode", view.Diff.Patch);
        var (request, plan) = await f.Prepare(lines: [5]); Assert.Equal("new\n", plan.Result); await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default);
        f.File = f.File with { IsUntracked = false, IndexStatus = "A" }; f.Index = $"100755 {Sha} 0\tfile.txt\0"; f.Baseline = Encoding.UTF8.GetBytes("new\nextra\n"); f.Diff = SelectedLinesBuilder.NewFilePatch("new\nextra\n");
        (request, plan) = await f.Prepare("staged", [5, 6]); Assert.True(plan.RemovesEntry);
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default);
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "update-index", "--force-remove", "--", "file.txt" })), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("new\nextra\n", await System.IO.File.ReadAllTextAsync(f.Full));
    }
    [Fact]
    public async Task SupportsPartialAndCompleteDeletionAndRestoresTheOriginalExecutableMode()
    {
        using var f = new Fixture(); System.IO.File.Delete(f.Full); f.File = f.File with { WorkTreeStatus = "D" }; f.Diff = "diff --git a/file.txt b/file.txt\ndeleted file mode 100755\n--- a/file.txt\n+++ /dev/null\n@@ -1 +0,0 @@\n-old\n";
        var (request, plan) = await f.Prepare(lines: [5]); Assert.True(plan.RemovesEntry); await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default);
        f.Index = ""; f.File = f.File with { IndexStatus = "D", WorkTreeStatus = "." };
        (request, plan) = await f.Prepare("staged", [5]); Assert.False(plan.RemovesEntry); Assert.Equal("old\n", plan.Result);
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default);
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args.Contains("--cacheinfo") && args.Contains("100755")), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(System.IO.File.Exists(f.Full));
    }
    [Theory]
    [InlineData("working")] [InlineData("index")] [InlineData("head")] [InlineData("patch")]
    public async Task RejectsChangedSnapshotsAndPlansWithoutTouchingTheIndex(string target)
    {
        using var f = new Fixture(); var (request, plan) = await f.Prepare();
        if (target == "working") await System.IO.File.AppendAllTextAsync(f.Full, "more");
        if (target == "index") f.Index += $"100644 {Sha} 0\tother.txt\0";
        if (target == "head") f.Head = new string('b', 40);
        if (target == "patch") f.Diff += "\n";
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.PrepareAsync(f.Root, request, default));
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default)); f.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task RejectsWrongTokensAlteredSelectionAndExternalChangesDuringHashing()
    {
        using var f = new Fixture(); var (request, plan) = await f.Prepare();
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request, new string('F', 64)), default));
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request with { Lines = [5] }, plan.Token), default));
        f.Race = true; await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default));
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args[0] == "update-index"), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ReportsMutationFailureWithoutRetryingAndCleansItsOwnTemporary(bool hash)
    {
        using var f = new Fixture(); var (request, plan) = await f.Prepare(); f.FailHash = hash; f.FailUpdate = !hash;
        await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default));
        Assert.Equal(hash ? 1 : 2, f.Writer.Invocations.Count); var args = (IReadOnlyList<string>)f.Writer.Invocations[0].Arguments[1]; Assert.False(System.IO.File.Exists(args[^1]));
    }
    [Theory]
    [InlineData("MERGE_HEAD")] [InlineData("REVERT_HEAD")] [InlineData("CHERRY_PICK_HEAD")] [InlineData("BISECT_LOG")] [InlineData("rebase-merge")] [InlineData("rebase-apply")]
    public async Task DefersPendingOperationsToTheirExistingFlow(string marker)
    {
        using var f = new Fixture(); System.IO.File.WriteAllText(Path.Combine(f.Root, ".git", marker), "pending");
        var view = await f.Editor.ReadAsync(f.Root, f.Request(), default); Assert.False(view.CanSelect); Assert.NotEmpty(view.Reason);
        await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.PrepareAsync(f.Root, new(f.Request(), [1], view.Token), default));
    }
    [Fact]
    public async Task ExplainsBinaryOversizedRenamedMetadataAndNonregularFiles()
    {
        using var f = new Fixture();
        foreach (var bytes in new[] { new byte[] { 0 }, new byte[] { 255 }, new byte[200001] })
        {
            await System.IO.File.WriteAllBytesAsync(f.Full, bytes); var view = await f.Editor.ReadAsync(f.Root, f.Request(), default); Assert.False(view.CanSelect); Assert.NotEmpty(view.Reason);
        }
        await System.IO.File.WriteAllTextAsync(f.Full, "new\n");
        foreach (var bytes in new[] { new byte[] { 0 }, new byte[] { 255 }, new byte[200001] }) { f.Baseline = bytes; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect); }
        f.Baseline = Encoding.UTF8.GetBytes("old\n");
        var original = f.File;
        foreach (var file in new[] { original with { IsConflicted = true }, original with { IsSubmodule = true }, original with { OriginalPath = "old.txt" } }) { f.File = file; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect); }
        f.File = new("file.txt", null, "M", "M", false, false, false);
        foreach (var index in new[] { $"120000 {Sha} 0\tfile.txt\0", $"100644 {Sha} 2\tfile.txt\0" }) { f.Index = index; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect); }
        f.Index = $"100644 {Sha} 0\tfile.txt\0";
        foreach (var patch in new[] { "Binary files a and b differ\n", "GIT binary patch\n", "diff --git a/x b/x\nold mode 100644\nnew mode 100755\n", new string('x', 200001), string.Concat(Enumerable.Repeat("\n", 6001)) }) { f.Diff = patch; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect); }
        f.Diff = Patch; System.IO.File.Delete(f.Full); Directory.CreateDirectory(f.Full); Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect);
    }
    [Fact]
    public async Task RejectsUnsupportedCleanFilterOutputAndKeepsOtherConflictsOutOfTheSelectiveFlow()
    {
        using var f = new Fixture(); f.Status = f.Status with { Conflicted = 1 }; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect);
        f.Status = f.Status with { Conflicted = 0 }; f.Index = ""; f.File = f.File with { IsUntracked = true };
        foreach (var bytes in new[] { new byte[] { 0 }, new byte[] { 255 }, new byte[200001] }) { f.Baseline = bytes; Assert.False((await f.Editor.ReadAsync(f.Root, f.Request(), default)).CanSelect); }
        var call = (IReadOnlyList<string>)f.Writer.Invocations[0].Arguments[1]; Assert.Contains("--path=file.txt", call); Assert.False(System.IO.File.Exists(call[^1]));
        f.FailHash = true; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default));
    }
    [Fact]
    public async Task RejectsInvalidOrTruncatedHashesAndHonorsCancellationBeforeMutation()
    {
        using var f = new Fixture(); var (request, plan) = await f.Prepare();
        foreach (var output in new[] { new LimitedGitOutput(0, "invalid", false), new LimitedGitOutput(0, Sha, true) })
        {
            f.Writer.Setup(x => x.RunAsync(f.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(output);
            await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default));
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var count = f.Writer.Invocations.Count;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), cancelled.Token)); Assert.Equal(count, f.Writer.Invocations.Count);
    }
    [Fact]
    public async Task RejectsMissingIneligibleUnsafeMalformedAndUnavailableStates()
    {
        using var f = new Fixture(); f.Missing = true; await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default)); f.Missing = false;
        foreach (var status in new[] { ".", " ", "" }) { f.File = f.File with { WorkTreeStatus = status }; await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default)); }
        foreach (var status in new[] { ".", "?", " ", "" }) { f.File = f.File with { IndexStatus = status }; await Assert.ThrowsAsync<ConflictAppException>(() => f.Editor.ReadAsync(f.Root, f.Request("staged"), default)); }
        f.File = f.File with { IndexStatus = "M", WorkTreeStatus = "M" };
        foreach (var index in new[] { "bad", "100644 invalid 0\tfile.txt\0", $"100644 {Sha}\tfile.txt\0" }) { f.Index = index; await Assert.ThrowsAsync<ValidationAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default)); }
        f.Index = ""; await Assert.ThrowsAsync<ValidationAppException>(() => f.Editor.ReadAsync(f.Root, f.Request() with { Path = "../outside" }, default));
        f.ExitCode = 1; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default)); f.ExitCode = 0; f.Truncated = true;
        await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default)); f.Truncated = false;
        f.Inspector.Setup(x => x.InspectAsync(f.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("wrong", Path.Combine(f.Root, "other"), ""));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Editor.ReadAsync(f.Root, f.Request(), default));
    }
    [Fact]
    public async Task DiscardsOnlyTheSelectedWorkingChangesWithoutUpdatingTheIndex()
    {
        using var f = new Fixture(); var (request, _) = await f.Prepare(); request = request with { Operation = "discard" };
        var plan = await f.Editor.PrepareAsync(f.Root, request, default); Assert.Equal("old\nextra\n", plan.Result); Assert.Equal("discard", plan.Operation);
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default); Assert.Equal("old\nextra\n", await System.IO.File.ReadAllTextAsync(f.Full)); f.Writer.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task PreservesCrLfDuringDiscardAndRejectsSmudgedOrMixedLineEndings()
    {
        using var f = new Fixture(); await System.IO.File.WriteAllTextAsync(f.Full, "new\r\nextra\r\n");
        var (request, _) = await f.Prepare(); request = request with { Operation = "discard" }; var plan = await f.Editor.PrepareAsync(f.Root, request, default); Assert.Equal("old\r\nextra\r\n", plan.Result);
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default); Assert.Equal(Encoding.UTF8.GetBytes(plan.Result), await System.IO.File.ReadAllBytesAsync(f.Full));
        foreach (var content in new[] { "new\r\nextra\n", "filter output" })
        {
            await System.IO.File.WriteAllTextAsync(f.Full, content); var view = await f.Editor.ReadAsync(f.Root, f.Request(), default); await Assert.ThrowsAsync<ValidationAppException>(() => f.Editor.PrepareAsync(f.Root, new(f.Request(), [4], view.Token, "discard"), default));
        }
    }
    [Fact]
    public async Task CanDiscardAnEntireUntrackedFileAndExportASelectionWithoutStagingIt()
    {
        using var f = new Fixture(); f.Index = ""; f.Baseline = Encoding.UTF8.GetBytes("new\nextra\n"); f.File = f.File with { IsUntracked = true };
        var (request, _) = await f.Prepare(lines: [5, 6]); request = request with { Operation = "discard" }; var plan = await f.Editor.PrepareAsync(f.Root, request, default); Assert.True(plan.RemovesEntry);
        await f.Editor.ExecuteAsync(f.Root, new(request, plan.Token), default); Assert.False(System.IO.File.Exists(f.Full));
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args.Contains("update-index")), It.IsAny<CancellationToken>()), Times.Never);
        await System.IO.File.WriteAllTextAsync(f.Full, "new\nextra\n"); var view = await f.Editor.ReadAsync(f.Root, f.Request(), default);
        plan = await f.Editor.PrepareAsync(f.Root, new(f.Request(), [5], view.Token, ExportPatch: true), default); Assert.Contains("new file mode", plan.Patch); Assert.Contains("b/file.txt", plan.Patch);
    }
}
