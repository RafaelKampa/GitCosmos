using System.Diagnostics;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using Moq;

namespace GitCosmos.Application.Tests;

public class GitRepositoryTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Inspector_ValidatesAbsoluteExistingPathsAndGitErrors(string language)
    {
        RequestLanguage.Current = language;
        var directory = Directory.CreateTempSubdirectory("GitCosmos-probe-");
        try
        {
            using var source = new CancellationTokenSource();
            var runner = new Mock<IGitProcessRunner>(MockBehavior.Strict);
            var inspector = new GitRepositoryInspector(runner.Object);
            foreach (var invalid in new[] { "relative", Path.Combine(directory.FullName, "missing") })
            {
                var error = await Assert.ThrowsAsync<ValidationAppException>(() => inspector.InspectAsync(invalid));
                Assert.Contains(LocalizedText.Get("repository.invalid_path"), error.Errors);
            }
            runner.Setup(x => x.RunAsync(directory.FullName, It.Is<IReadOnlyList<string>>(a =>
                a.SequenceEqual(new[] { "rev-parse", "--path-format=absolute", "--show-toplevel" })), source.Token))
                .ReturnsAsync(new GitCommandResult(128, "private diagnostic"));
            var notGit = await Assert.ThrowsAsync<ValidationAppException>(() => inspector.InspectAsync(directory.FullName, source.Token));
            Assert.Contains(LocalizedText.Get("repository.invalid_git"), notGit.Errors);
            foreach (var output in new[] { "\0", "", "relative", Path.Combine(directory.FullName, "missing") })
            {
                runner.Setup(x => x.RunAsync(directory.FullName, It.IsAny<IReadOnlyList<string>>(), source.Token))
                    .ReturnsAsync(new GitCommandResult(0, output));
                await Assert.ThrowsAsync<ValidationAppException>(() => inspector.InspectAsync(directory.FullName, source.Token));
            }
            runner.Setup(x => x.RunAsync(directory.FullName, It.IsAny<IReadOnlyList<string>>(), source.Token))
                .ReturnsAsync(new GitCommandResult(0, directory.FullName + "\r\n"));
            var result = await inspector.InspectAsync(" " + directory.FullName + " ", source.Token);
            Assert.Equal(directory.Name, result.Name);
            Assert.Equal(directory.FullName, result.LocalPath);
            Assert.Equal(directory.FullName.ToUpperInvariant(), result.PathKey);
            source.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inspector.InspectAsync(directory.FullName, source.Token));
        }
        finally { Directory.Delete(directory.FullName); }
    }

    [Fact]
    public async Task RealGit_AcceptsEmptyWorkingTreesNestedPathsAndLinkedWorktreesAndRejectsBareRepos()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        var inspector = new GitRepositoryInspector(new GitProcessRunner("git", TimeSpan.FromSeconds(10)));
        var nested = Directory.CreateDirectory(Path.Combine(fixture.RepositoryPath, "subfolder")).FullName;
        var root = await inspector.InspectAsync(fixture.RepositoryPath + Path.DirectorySeparatorChar);
        var child = await inspector.InspectAsync(nested);
        Assert.Equal(root, child);
        Assert.Equal(fixture.RepositoryPath.ToUpperInvariant(), root.PathKey);
        var bare = Path.Combine(fixture.Root, "bare");
        await fixture.RunAsync(fixture.Root, "init", "--quiet", "--bare", "--template=", bare);
        await Assert.ThrowsAsync<ValidationAppException>(() => inspector.InspectAsync(bare));
        var tree = (await fixture.RunAsync(fixture.RepositoryPath, "mktree")).Trim();
        var commit = (await fixture.RunAsync(fixture.RepositoryPath, "-c", "user.name=GitCosmos Tests", "-c", "user.email=tests@example.invalid",
            "-c", "commit.gpgsign=false", "commit-tree", tree, "-m", "fixture")).Trim();
        var worktree = Path.Combine(fixture.Root, "worktree");
        await fixture.RunAsync(fixture.RepositoryPath, "worktree", "add", "--quiet", "--detach", worktree, commit);
        var linked = await inspector.InspectAsync(worktree);
        Assert.Equal(worktree.ToUpperInvariant(), linked.PathKey);
        Assert.NotEqual(root.PathKey, linked.PathKey);
        Assert.True(File.Exists(Path.Combine(worktree, ".git")));
        Assert.Equal(root.PathKey, (await inspector.InspectAsync(fixture.RepositoryPath.ToUpperInvariant())).PathKey);
        var failed = await new GitProcessRunner("git", TimeSpan.FromSeconds(10)).RunAsync(fixture.RepositoryPath, ["rev-parse", "--verify", "missing-ref"]);
        Assert.NotEqual(0, failed.ExitCode);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task Runner_ReportsMissingGitWithoutExposingNativeDiagnostics(string language)
    {
        RequestLanguage.Current = language;
        var missing = new GitProcessRunner(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), TimeSpan.FromSeconds(10));
        var error = await Assert.ThrowsAsync<OperationFailedAppException>(() => missing.RunAsync(Path.GetTempPath(), ["--version"]));
        Assert.Equal(LocalizedText.Get("repository.git_unavailable"), error.Message);
        var cancelled = new CancellationToken(canceled: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => missing.RunAsync(Path.GetTempPath(), ["--version"], cancelled));
    }

    [Fact]
    public async Task Runner_CancelsAndTimesOutBlockedReadOnlyGitProcesses()
    {
        await using var fixture = await GitTestDirectory.CreateAsync();
        // --batch waits for standard input; the runner owns that redirected pipe.
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitProcessRunner("git", TimeSpan.FromSeconds(10)).RunAsync(fixture.RepositoryPath, ["cat-file", "--batch"], source.Token));
        var timeout = await Assert.ThrowsAsync<OperationFailedAppException>(() =>
            new GitProcessRunner("git", TimeSpan.FromMilliseconds(150)).RunAsync(fixture.RepositoryPath, ["cat-file", "--batch"]));
        Assert.Equal(LocalizedText.Get("repository.git_timeout"), timeout.Message);
        var version = await new GitProcessRunner("git", TimeSpan.FromSeconds(10)).RunAsync(fixture.RepositoryPath, ["--version"]);
        Assert.Equal(0, version.ExitCode); Assert.Contains("git version", version.Output);
    }
}

internal sealed class GitTestDirectory : IAsyncDisposable
{
    public string Root { get; } = Directory.CreateTempSubdirectory("GitCosmos-repositories-").FullName;
    public string RepositoryPath => Path.Combine(Root, "repo espaço ' sample");

    public static async Task<GitTestDirectory> CreateAsync()
    {
        var fixture = new GitTestDirectory();
        try
        {
            await fixture.RunAsync(fixture.Root, "init", "--quiet", "--template=", "--object-format=sha1", fixture.RepositoryPath);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async Task<string> RunAsync(string directory, params string[] arguments)
    {
        using var process = new Process { StartInfo = new("git")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        } };
        foreach (var variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES" })
            process.StartInfo.Environment.Remove(variable);
        foreach (var argument in new[] { "-c", "core.hooksPath=" + Path.Combine(Root, "no-hooks"), "-c", "core.fsmonitor=false" }.Concat(arguments))
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await error);
        return await output;
    }

    public ValueTask DisposeAsync()
    {
        // Only the unique directory allocated by this fixture is recursively removed.
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(Root, recursive: true);
        return ValueTask.CompletedTask;
    }
}
