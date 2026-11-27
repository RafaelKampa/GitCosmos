using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;
namespace GitCosmos.Application.Tests;

public class RepositoryConflictsTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("GitCosmos-conflicts-").FullName;
        public string Full => Path.Combine(Root, "file.txt");
        public Mock<IBoundedGitProcessRunner> Reader { get; } = new();
        public Mock<IGitMutationRunner> Writer { get; } = new();
        public Mock<IGitRepositoryInspector> Inspector { get; } = new();
        public Mock<IGitBlobReader> Blobs { get; } = new();
        public Mock<ICommitFileDesktop> Desktop { get; } = new();
        public string Stages = $"100644 {Sha} 1\tfile.txt\0" + $"100644 {new string('b', 40)} 2\tfile.txt\0" + $"100644 {new string('c', 40)} 3\tfile.txt\0";
        public string Index = "index"; public bool Resolved; public int ExitCode; public bool Truncated; public string PathResult = "file.txt";
        public string? UnmergedOverride; public bool FailWriter;
        public RepositoryConflicts Conflicts { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, ".git")); System.IO.File.WriteAllText(Full, "local result\r\n");
            Inspector.Setup(x => x.InspectAsync(Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", Root, Root));
            Reader.Setup(x => x.RunLimitedAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, IReadOnlyList<string> args, int _, CancellationToken _) => new LimitedGitOutput(ExitCode, args.Contains("--git-common-dir") || args.Contains("--absolute-git-dir") ? Path.Combine(Root, ".git") : args.Contains("--unmerged") ? UnmergedOverride ?? (Resolved ? "" : Stages.Replace("file.txt", PathResult)) : args.Contains("ls-files") ? Index : Sha, Truncated));
            Blobs.Setup(x => x.ReadAsync(Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, string oid, CancellationToken _) => Encoding.UTF8.GetBytes(oid[0] == 'a' ? "base\n" : oid[0] == 'b' ? "ours\n" : "theirs\n"));
            Writer.Setup(x => x.RunAsync(Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => { if (FailWriter) return new LimitedGitOutput(1, "private", false); Resolved = true; return new LimitedGitOutput(0, "", false); });
            Conflicts = new(Reader.Object, Blobs.Object, Writer.Object, Inspector.Object, Desktop.Object);
        }
        public RepositoryConflictRequest File => new(1, "file.txt");
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Fact]
    public async Task ReadsBaseOursTheirsAndWorkingBytesAndIdentifiesMergeAndRebase()
    {
        using var f = new Fixture();
        var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default);
        Assert.Equal("base\n", view.Base.Text); Assert.Equal("ours\n", view.Ours.Text); Assert.Equal("theirs\n", view.Theirs.Text); Assert.Equal("local result\r\n", view.Working.Text); Assert.Empty(view.Operation); Assert.Equal(64, view.Token.Length);
        System.IO.File.WriteAllText(Path.Combine(f.Root, ".git", "MERGE_HEAD"), Sha); Assert.Equal("merge", (await f.Conflicts.ReadAsync(f.Root, "file.txt", default)).Operation);
        Directory.CreateDirectory(Path.Combine(f.Root, ".git", "rebase-merge")); System.IO.File.WriteAllText(Path.Combine(f.Root, ".git", "rebase-merge", "head-name"), "refs/heads/feature");
        Assert.Equal("rebase", (await f.Conflicts.ReadAsync(f.Root, "file.txt", default)).Operation);
    }
    [Theory]
    [InlineData("edit", "edited\r\n")]
    [InlineData("ours", "ours\n")]
    [InlineData("theirs", "theirs\n")]
    [InlineData("stage", "local result\r\n")]
    [InlineData("delete", null)]
    public async Task ResolvesOnlyTheReviewedLiteralPath(string action, string? expected)
    {
        using var f = new Fixture(); var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default);
        var request = new ResolveConflictRequest(f.File, action, "edited\r\n", view.Token); var plan = await f.Conflicts.PrepareAsync(f.Root, request, default);
        f.Writer.VerifyNoOtherCalls(); Assert.Equal(expected is null, plan.RemovesFile);
        await f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default);
        if (expected is null) Assert.False(System.IO.File.Exists(f.Full)); else Assert.Equal(expected, await System.IO.File.ReadAllTextAsync(f.Full));
        f.Writer.Verify(x => x.RunAsync(f.Root, It.Is<IReadOnlyList<string>>(args => args.SequenceEqual(new[] { "--literal-pathspecs", "add", "--all", "--", "file.txt" })), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(Directory.GetFiles(f.Root, ".gitcosmos-resolution-*"));
    }
    [Fact]
    public async Task HandlesAbsentSidesBinaryAndNonUtf8WithoutConvertingBytes()
    {
        using var f = new Fixture(); f.Stages = $"100644 {Sha} 2\tfile.txt\0"; System.IO.File.Delete(f.Full);
        var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); Assert.Equal("absent", view.Base.Kind); Assert.Equal("absent", view.Theirs.Kind); Assert.Equal("absent", view.Working.Kind);
        var request = new ResolveConflictRequest(f.File, "theirs", Token: view.Token); var plan = await f.Conflicts.PrepareAsync(f.Root, request, default); Assert.True(plan.RemovesFile);
        var bytes = new byte[] { 0, 255, 1 }; await System.IO.File.WriteAllBytesAsync(f.Full, bytes);
        f.Blobs.Setup(x => x.ReadAsync(f.Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(bytes);
        view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); Assert.Equal("binary", view.Ours.Kind); Assert.Equal("binary", view.Working.Kind);
        request = new(f.File, "ours", Token: view.Token); plan = await f.Conflicts.PrepareAsync(f.Root, request, default); await f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default); Assert.Equal(bytes, await System.IO.File.ReadAllBytesAsync(f.Full));
        f.Resolved = false; f.Blobs.Setup(x => x.ReadAsync(f.Root, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new byte[] { 255 }); Assert.Equal("binary", (await f.Conflicts.ReadAsync(f.Root, "file.txt", default)).Ours.Kind);
    }
    [Fact]
    public async Task RefusesStaleReadsPlansAndUnresolvedMarkersAndDoesNotAutoRetryFailedStage()
    {
        using var f = new Fixture(); var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); var request = new ResolveConflictRequest(f.File, "edit", "resolved", view.Token);
        await System.IO.File.WriteAllTextAsync(f.Full, "changed"); await Assert.ThrowsAsync<ConflictAppException>(() => f.Conflicts.PrepareAsync(f.Root, request, default));
        view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); request = request with { Token = view.Token }; var plan = await f.Conflicts.PrepareAsync(f.Root, request, default);
        f.Index = "changed index"; await Assert.ThrowsAsync<ConflictAppException>(() => f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default));
        view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); request = request with { Token = view.Token };
        foreach (var markers in new[] { "<<<<<<< HEAD\na\n=======\nb\n>>>>>>> branch", "||||||| base\r\n", "=======\r\n" })
            await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.PrepareAsync(f.Root, request with { Content = markers }, default));
        plan = await f.Conflicts.PrepareAsync(f.Root, request, default); f.FailWriter = true;
        await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default));
        Assert.Equal("resolved", await System.IO.File.ReadAllTextAsync(f.Full)); f.Writer.Verify(x => x.RunAsync(f.Root, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData("broken")]
    [InlineData("100644 nope 1\tfile.txt\0")]
    [InlineData("120000 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 2\tfile.txt\0")]
    [InlineData("160000 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 2\tfile.txt\0")]
    [InlineData("100644 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 4\tfile.txt\0")]
    [InlineData("100644 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa no\tfile.txt\0")]
    [InlineData("100644 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 2\tfile.txt\0" + "100644 aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa 2\tfile.txt\0")]
    public async Task RejectsMalformedOrUnsupportedIndexStages(string stages)
    {
        using var f = new Fixture(); f.Stages = stages; await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default));
    }
    [Fact]
    public async Task RejectsUnavailableAndUnsafeStatesAndBoundsEditableContent()
    {
        using var f = new Fixture();
        f.PathResult = "other.txt"; await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); f.PathResult = "file.txt";
        f.Resolved = true; await Assert.ThrowsAsync<ConflictAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); f.Resolved = false;
        f.ExitCode = 1; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); f.ExitCode = 0;
        f.Truncated = true; await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); f.Truncated = false;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "../escape", default));
        System.IO.File.Delete(f.Full); Directory.CreateDirectory(f.Full); await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); Directory.Delete(f.Full);
        System.IO.File.WriteAllText(Path.Combine(f.Root, ".git", "CHERRY_PICK_HEAD"), Sha); await Assert.ThrowsAsync<ConflictAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default)); System.IO.File.Delete(Path.Combine(f.Root, ".git", "CHERRY_PICK_HEAD"));
        await System.IO.File.WriteAllBytesAsync(f.Full, new byte[GitBlobReader.MaximumBytes + 1]); await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default));
        await System.IO.File.WriteAllTextAsync(f.Full, new string('x', 200001)); var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); Assert.Equal("tooLarge", view.Working.Kind);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.PrepareAsync(f.Root, new(f.File, "edit", "text", view.Token), default));
        await System.IO.File.WriteAllTextAsync(f.Full, "text"); view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default);
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.PrepareAsync(f.Root, new(f.File, "invalid", Token: view.Token), default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.PrepareAsync(f.Root, new(f.File, "edit", new string('é', 150000), view.Token), default));
    }
    [Fact]
    public async Task FingerprintChangesWithOperationProgressAndRejectsOversizedMetadata()
    {
        using var f = new Fixture(); var rebase = Directory.CreateDirectory(Path.Combine(f.Root, ".git", "rebase-apply")).FullName;
        await System.IO.File.WriteAllTextAsync(Path.Combine(rebase, "next"), "1"); var first = await f.Conflicts.ReadAsync(f.Root, "file.txt", default);
        Assert.Equal("rebase", first.Operation); await System.IO.File.WriteAllTextAsync(Path.Combine(rebase, "next"), "2");
        Assert.NotEqual(first.Token, (await f.Conflicts.ReadAsync(f.Root, "file.txt", default)).Token);
        await System.IO.File.WriteAllTextAsync(Path.Combine(rebase, "next"), new string('x', 200001));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default));
    }
    [Fact]
    public async Task CreatesMissingParentsAndAllowsExplicitDeletionForAnAbsentOursSide()
    {
        using var f = new Fixture(); f.PathResult = "nested/file.txt"; f.Stages = $"100644 {Sha} 3\tfile.txt\0";
        var view = await f.Conflicts.ReadAsync(f.Root, "nested/file.txt", default); var file = new RepositoryConflictRequest(1, "nested/file.txt");
        Assert.Equal("absent", view.Ours.Kind);
        var plan = await f.Conflicts.PrepareAsync(f.Root, new(file, "ours", Token: view.Token), default); Assert.True(plan.RemovesFile);
        var request = new ResolveConflictRequest(file, "edit", "new file\n", view.Token); plan = await f.Conflicts.PrepareAsync(f.Root, request, default);
        await f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default); Assert.Equal("new file\n", await System.IO.File.ReadAllTextAsync(Path.Combine(f.Root, "nested", "file.txt")));
    }
    [Fact]
    public async Task OpensOnlyAnExistingValidatedWorkingFileForExternalEditing()
    {
        using var f = new Fixture(); await f.Conflicts.ExternalAsync(f.Root, f.File, default); f.Desktop.Verify(x => x.Edit(f.Full), Times.Once);
        System.IO.File.Delete(f.Full); await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ExternalAsync(f.Root, f.File, default));
        f.Inspector.Setup(x => x.InspectAsync(f.Root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("other", Path.Combine(f.Root, "other"), ""));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Conflicts.ReadAsync(f.Root, "file.txt", default));
    }
    [Fact]
    public async Task DetectsAnIndexThatStillContainsTheConflictAfterGitAdd()
    {
        using var f = new Fixture(); f.UnmergedOverride = f.Stages;
        var view = await f.Conflicts.ReadAsync(f.Root, "file.txt", default); var request = new ResolveConflictRequest(f.File, "stage", Token: view.Token);
        var plan = await f.Conflicts.PrepareAsync(f.Root, request, default);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => f.Conflicts.ResolveAsync(f.Root, request with { Token = plan.Token }, default));
    }
    [Theory]
    [InlineData("merge")]
    [InlineData("rebase")]
    public async Task RealGitResolvesOnlyOnePathWithoutCommittingOrContinuingTheOperation(string operation)
    {
        await using var f = await GitTestDirectory.CreateAsync(); var path = Path.Combine(f.RepositoryPath, "conflict.txt");
        await f.RunAsync(f.RepositoryPath, "config", "user.name", "GitCosmos Tests"); await f.RunAsync(f.RepositoryPath, "config", "user.email", "test@example.invalid");
        await f.RunAsync(f.RepositoryPath, "config", "commit.gpgsign", "false");
        await f.RunAsync(f.RepositoryPath, "config", "core.autocrlf", "false"); await f.RunAsync(f.RepositoryPath, "config", "core.hooksPath", Path.Combine(f.Root, "no-hooks"));
        await System.IO.File.WriteAllTextAsync(path, "base\n"); await f.RunAsync(f.RepositoryPath, "add", "--", "conflict.txt"); await f.RunAsync(f.RepositoryPath, "commit", "-qm", "base");
        var start = (await f.RunAsync(f.RepositoryPath, "rev-parse", "HEAD")).Trim(); await f.RunAsync(f.RepositoryPath, "checkout", "-qb", "conflict-side");
        await System.IO.File.WriteAllTextAsync(path, "incoming\n"); await f.RunAsync(f.RepositoryPath, "commit", "-qam", "incoming"); var side = (await f.RunAsync(f.RepositoryPath, "rev-parse", "HEAD")).Trim();
        await f.RunAsync(f.RepositoryPath, "checkout", "-qb", "conflict-main", start); await System.IO.File.WriteAllTextAsync(path, "local\n"); await f.RunAsync(f.RepositoryPath, "commit", "-qam", "local");
        var reader = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var writer = new GitMutationRunner();
        Assert.NotEqual(0, (await writer.RunAsync(f.RepositoryPath, operation == "merge" ? ["merge", "--no-edit", side] : ["-c", "core.editor=true", "rebase", "--no-autostash", side])).ExitCode);
        var head = (await f.RunAsync(f.RepositoryPath, "rev-parse", "HEAD")).Trim();
        var conflicts = new RepositoryConflicts(reader, new GitBlobReader("git", TimeSpan.FromSeconds(10)), writer, new GitRepositoryInspector(reader), Mock.Of<ICommitFileDesktop>());
        var view = await conflicts.ReadAsync(f.RepositoryPath, "conflict.txt", default); Assert.Equal(operation, view.Operation); Assert.Contains("incoming", operation == "merge" ? view.Theirs.Text : view.Ours.Text);
        var request = new ResolveConflictRequest(new(1, "conflict.txt"), "edit", "resolved\n", view.Token); var plan = await conflicts.PrepareAsync(f.RepositoryPath, request, default);
        await conflicts.ResolveAsync(f.RepositoryPath, request with { Token = plan.Token }, default);
        Assert.Empty(await f.RunAsync(f.RepositoryPath, "ls-files", "--unmerged")); Assert.Equal(head, (await f.RunAsync(f.RepositoryPath, "rev-parse", "HEAD")).Trim());
        Assert.True(System.IO.File.Exists(Path.Combine(f.RepositoryPath, ".git", "MERGE_HEAD")) || Directory.Exists(Path.Combine(f.RepositoryPath, ".git", "rebase-merge")) || Directory.Exists(Path.Combine(f.RepositoryPath, ".git", "rebase-apply")));
        Assert.Equal("resolved\n", await f.RunAsync(f.RepositoryPath, "show", ":conflict.txt"));
    }
}
