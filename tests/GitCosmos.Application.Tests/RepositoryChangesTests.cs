using System.Text;
using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Infrastructure.Git;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GitCosmos.Application.Tests;

public class RepositoryChangesTests
{
    private const string Headers = "# branch.oid abc\0# branch.head main\0";
    private static string Entry(string xy, string path) => $"1 {xy} N... 100644 100644 100644 abc abc {path}\0";

    [Fact]
    public void ParserPreservesPathsStatusesAndRenameOrigins()
    {
        var output = Headers + "# future value\0! ignored\0" + Entry("MM", "folder/file ç with spaces.txt")
            + "? new\nfile\0"
            + "2 R. N... 100644 100644 100644 abc abc R100 renamed.txt\0? unusual original\0"
            + "u UU S... 100644 100644 100644 100644 abc abc abc conflict\0";
        var result = GitChangesParser.Parse(output);
        Assert.Equal(4, result.Status.ChangedFiles);
        Assert.Collection(result.Files,
            file => { Assert.Equal("folder/file ç with spaces.txt", file.Path); Assert.Equal("M", file.IndexStatus); Assert.Equal("M", file.WorkTreeStatus); Assert.False(file.IsUntracked); },
            file => { Assert.True(file.IsUntracked); Assert.Equal("new\nfile", file.Path); },
            file => { Assert.Equal("? unusual original", file.OriginalPath); Assert.Equal("renamed.txt", file.Path); },
            file => { Assert.True(file.IsConflicted); Assert.True(file.IsSubmodule); });
        Assert.Empty(GitChangesParser.Parse(Headers).Files);
    }

    [Theory]
    [InlineData("1 MM short")]
    [InlineData("1 M N... 100644 100644 100644 abc abc file")]
    [InlineData("1 MM N... 100644 100644 100644 abc abc ")]
    public void ParserRejectsIncompleteRecords(string entry) =>
        Assert.Throws<OperationFailedAppException>(() => GitChangesParser.Parse(Headers + entry + "\0"));

    [Fact]
    public async Task RealGitShowsWorkingStagedRenamedBinaryAndUntrackedChangesWithoutMutatingFilesOrIndex()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var root = fixture.RepositoryPath;
        var path = "file ç.txt";
        File.WriteAllText(Path.Combine(root, path), "before\n");
        File.WriteAllText(Path.Combine(root, "rename.txt"), "rename content\n");
        File.WriteAllBytes(Path.Combine(root, "binary.dat"), [0, 1, 2]);
        await fixture.RunAsync(root, "add", "--all");
        await fixture.RunAsync(root, "-c", "user.name=Tests", "-c", "user.email=test@example.invalid", "commit", "--quiet", "-m", "fixture");
        File.WriteAllText(Path.Combine(root, path), "staged\n");
        await fixture.RunAsync(root, "add", "--", path);
        File.WriteAllText(Path.Combine(root, path), "working\n");
        await fixture.RunAsync(root, "mv", "rename.txt", "renamed.txt");
        File.WriteAllBytes(Path.Combine(root, "binary.dat"), [0, 4, 5]);
        File.WriteAllText(Path.Combine(root, "new.txt"), "new file\n");
        var process = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
        var reader = new RepositoryChangesReader(process, new GitRepositoryInspector(process));
        var indexBefore = File.ReadAllBytes(Path.Combine(root, ".git", "index"));
        var changes = await reader.ReadAsync(root);
        Assert.Equal(4, changes.Files.Count);
        var staged = await reader.ReadDiffAsync(root, path, "staged");
        Assert.Equal("text", staged.Kind); Assert.Contains("-before", staged.Patch); Assert.Contains("+staged", staged.Patch);
        var working = await reader.ReadDiffAsync(root, path, "working");
        Assert.Contains("-staged", working.Patch); Assert.Contains("+working", working.Patch);
        Assert.Contains("rename from", (await reader.ReadDiffAsync(root, "renamed.txt", "staged")).Patch);
        Assert.Equal("binary", (await reader.ReadDiffAsync(root, "binary.dat", "working")).Kind);
        Assert.Contains("+new file", (await reader.ReadDiffAsync(root, "new.txt", "working")).Patch);
        Assert.Equal(indexBefore, File.ReadAllBytes(Path.Combine(root, ".git", "index")));
        Assert.Equal("working\n", File.ReadAllText(Path.Combine(root, path)));
    }

    [Fact]
    public async Task NewRepositorySupportsStagedDiffBeforeItsFirstCommit()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        File.WriteAllText(Path.Combine(fixture.RepositoryPath, "first.txt"), "first\n");
        await fixture.RunAsync(fixture.RepositoryPath, "add", "first.txt");
        var process = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
        var reader = new RepositoryChangesReader(process, new GitRepositoryInspector(process));
        Assert.False((await reader.ReadAsync(fixture.RepositoryPath)).Status.HasCommits);
        Assert.Contains("+first", (await reader.ReadDiffAsync(fixture.RepositoryPath, "first.txt", "staged")).Patch);
    }

    private static (RepositoryChangesReader Reader, Mock<IBoundedGitProcessRunner> Runner, Mock<IGitRepositoryInspector> Inspector) Fake(string root, string output)
    {
        var inspector = new Mock<IGitRepositoryInspector>();
        inspector.Setup(x => x.InspectAsync(root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("repo", root, root));
        var runner = new Mock<IBoundedGitProcessRunner>(MockBehavior.Strict);
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("status")), 1_000_000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, output, false));
        return (new(runner.Object, inspector.Object), runner, inspector);
    }

    [Theory]
    [InlineData("binary", "-\t-\tfile\0", "", false)]
    [InlineData("text", "1\t1\tfile\0", "@@ -1 +1 @@\n-old\n+new\n", false)]
    [InlineData("tooLarge", "1\t1\tfile\0", "truncated", true)]
    [InlineData("empty", "", "", false)]
    public async Task DiffUsesLiteralPathsNoExternalDriversAndBoundedOutput(string kind, string numbers, string patch, bool truncated)
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        const string path = "[special] file.txt";
        var (reader, runner, _) = Fake(root, Headers + Entry("MM", path));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--numstat") && a.Contains("--literal-pathspecs")
            && a.Contains("--no-ext-diff") && a.Contains("--no-textconv") && a.Contains("--cached") && a[a.Count - 1] == path), RepositoryChangesReader.MaximumDiffCharacters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, numbers, false));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--patch")), RepositoryChangesReader.MaximumDiffCharacters, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitedGitOutput(0, patch, truncated));
        Assert.Equal(kind, (await reader.ReadDiffAsync(root, path, "staged")).Kind);
    }

    [Fact]
    public async Task DiffDoesNotRenderExcessiveLineCounts()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        var (reader, runner, _) = Fake(root, Headers + Entry(".M", "file"));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--numstat")), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, "1\t0\tfile\0", false));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--patch")), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(0, string.Concat(Enumerable.Repeat("+line\n", 4001)), false));
        Assert.Equal("tooLarge", (await reader.ReadDiffAsync(root, "file", "working")).Kind);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("limit")]
    [InlineData("numbers")]
    [InlineData("patch")]
    [InlineData("root")]
    public async Task ReadFailuresRemainLocalizedAndReleaseTheReadSlot(string stage)
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        var (reader, runner, inspector) = Fake(root, Headers + Entry(".M", "file"));
        if (stage == "root") inspector.Setup(x => x.InspectAsync(root, It.IsAny<CancellationToken>())).ReturnsAsync(new LocalGitRepository("parent", fixture.Root, fixture.Root));
        if (stage is "status" or "limit") runner.Setup(x => x.RunLimitedAsync(root, It.IsAny<IReadOnlyList<string>>(), 1_000_000, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(stage == "status" ? 1 : 0, Headers, stage == "limit"));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--numstat")), RepositoryChangesReader.MaximumDiffCharacters, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(stage == "numbers" ? 1 : 0, "", false));
        runner.Setup(x => x.RunLimitedAsync(root, It.Is<IReadOnlyList<string>>(a => a.Contains("--patch")), RepositoryChangesReader.MaximumDiffCharacters, It.IsAny<CancellationToken>())).ReturnsAsync(new LimitedGitOutput(1, "private error", false));
        var exception = await Record.ExceptionAsync(() => reader.ReadDiffAsync(root, "file", "working"));
        Assert.IsAssignableFrom<AppException>(exception); Assert.DoesNotContain("private error", exception!.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../outside")]
    [InlineData(".git/config")]
    [InlineData("C:/outside")]
    [InlineData("file:stream")]
    [InlineData("/outside")]
    public async Task RejectsPathsOutsideWorkingFilesEvenIfReportedByAChangedStatus(string path)
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var (reader, _, _) = Fake(fixture.RepositoryPath, Headers + "? " + path + "\0");
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadDiffAsync(fixture.RepositoryPath, path, "working"));
    }

    [Theory]
    [InlineData("binary")]
    [InlineData("encoding")]
    [InlineData("large")]
    [InlineData("lines")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task UntrackedPreviewsAreBoundedAndDoNotReadUnsupportedContent(string kind)
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        var path = Path.Combine(root, "new");
        if (kind == "binary") File.WriteAllBytes(path, [0, 1]);
        if (kind == "encoding") File.WriteAllBytes(path, [0xFF, 0xFE, 0xFF]);
        if (kind == "large") File.WriteAllText(path, new string('x', RepositoryChangesReader.MaximumDiffCharacters + 1));
        if (kind == "lines") File.WriteAllText(path, new string('\n', 4001));
        if (kind == "empty") File.WriteAllText(path, "");
        var (reader, _, _) = Fake(root, Headers + "? new\0");
        if (kind == "missing") await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadDiffAsync(root, "new", "working"));
        else Assert.Equal(kind == "empty" ? "empty" : kind is "binary" or "encoding" ? "binary" : "tooLarge", (await reader.ReadDiffAsync(root, "new", "working")).Kind);
    }

    [Fact]
    public async Task ConflictSubmoduleAndStaleSelectionsNeverRunDiffCommands()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        var output = Headers + "u UU N... 100644 100644 100644 100644 abc abc abc conflict\0" + "1 .M S.M. 160000 160000 160000 abc abc module\0";
        var (reader, _, _) = Fake(root, output);
        Assert.Equal("conflict", (await reader.ReadDiffAsync(root, "conflict", "working")).Kind);
        Assert.Equal("submodule", (await reader.ReadDiffAsync(root, "module", "working")).Kind);
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadDiffAsync(root, "conflict", "staged"));
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadDiffAsync(root, "missing", "working"));
        await Assert.ThrowsAsync<ConflictAppException>(() => reader.ReadDiffAsync(root, "module", "staged"));
        await Assert.ThrowsAsync<ValidationAppException>(() => reader.ReadDiffAsync(root, "module", "invalid"));
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(root, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadDiffAsync(root, "module", "working", source.Token));
    }

    [Fact]
    public async Task BoundedRunnerCapsOutputAndDrainsStderr()
    {
        var root = Directory.CreateTempSubdirectory("GitCosmos-bounded-");
        try
        {
            var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10));
            var result = await runner.RunLimitedAsync(root.FullName, ["--version"], 4);
            Assert.Equal(0, result.ExitCode); Assert.Equal(4, result.Output.Length); Assert.True(result.Truncated);
            var failed = await runner.RunLimitedAsync(root.FullName, ["unknown-gitcosmos-command"], 0);
            Assert.NotEqual(0, failed.ExitCode); Assert.Empty(failed.Output);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunLimitedAsync(root.FullName, [], -1));
        }
        finally { Directory.Delete(root.FullName); }
    }
}
