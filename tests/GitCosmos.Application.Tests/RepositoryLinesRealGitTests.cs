using System.Text;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Infrastructure.Git;
namespace GitCosmos.Application.Tests;
public class RepositoryLinesRealGitTests
{
    [Theory]
    [InlineData("\n", true)] [InlineData("\r\n", true)] [InlineData("\n", false)]
    public async Task RealGitStagesAndUnstagesIndividualLinesPreservingWorkingBytesAndHead(string newline, bool finalNewline)
    {
        await using var f = await GitTestDirectory.CreateAsync(); var root = f.RepositoryPath; const string path = "a & b ' café.txt"; var full = Path.Combine(root, path);
        await Configure(f);
        var old = "first" + newline + "old" + newline + "last" + (finalNewline ? newline : "");
        await File.WriteAllTextAsync(full, old, new UTF8Encoding(false)); await f.RunAsync(root, "add", "--", path); await f.RunAsync(root, "commit", "-qm", "initial");
        var head = await f.RunAsync(root, "rev-parse", "HEAD");
        var working = "first" + newline + "new" + newline + "extra" + newline + "last" + (finalNewline ? newline : "");
        await File.WriteAllTextAsync(full, working, new UTF8Encoding(false));
        var editor = Create(root); var file = new RepositoryLinesRequest(1, path, "working"); var snapshot = await editor.ReadAsync(root, file, default);
        var addition = Find(snapshot.Diff.Patch, "+extra" + (newline == "\r\n" ? "\r" : ""));
        var request = new PrepareLinesRequest(file, [addition], snapshot.Token); var plan = await editor.PrepareAsync(root, request, default);
        Assert.Equal("first" + newline + "old" + newline + "extra" + newline + "last" + (finalNewline ? newline : ""), plan.Result);
        await editor.ExecuteAsync(root, new(request, plan.Token), default);
        var reader = new GitBlobReader("git", TimeSpan.FromSeconds(10)); var oid = (await f.RunAsync(root, "rev-parse", ":" + path)).Trim();
        Assert.Equal(Encoding.UTF8.GetBytes(plan.Result), await reader.ReadAsync(root, oid, default)); Assert.Equal(Encoding.UTF8.GetBytes(working), await File.ReadAllBytesAsync(full));
        file = file with { Area = "staged" }; snapshot = await editor.ReadAsync(root, file, default); request = new(file, [Find(snapshot.Diff.Patch, "+extra" + (newline == "\r\n" ? "\r" : ""))], snapshot.Token); plan = await editor.PrepareAsync(root, request, default);
        await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Equal(old, plan.Result); Assert.Equal(head, await f.RunAsync(root, "rev-parse", "HEAD")); Assert.Empty(await f.RunAsync(root, "diff", "--cached")); Assert.Equal(Encoding.UTF8.GetBytes(working), await File.ReadAllBytesAsync(full));
    }
    [Fact]
    public async Task RealGitHandlesAnUnbornAdditionAndPartialDeletionWithoutCreatingCommitsOrRestoringFiles()
    {
        await using var f = await GitTestDirectory.CreateAsync(); await Configure(f); var root = f.RepositoryPath; var full = Path.Combine(root, "new.txt");
        await File.WriteAllTextAsync(full, "one\ntwo\n"); var editor = Create(root); var file = new RepositoryLinesRequest(1, "new.txt", "working");
        var snapshot = await editor.ReadAsync(root, file, default); var request = new PrepareLinesRequest(file, [Find(snapshot.Diff.Patch, "+one")], snapshot.Token); var plan = await editor.PrepareAsync(root, request, default); await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Equal("one\n", await f.RunAsync(root, "show", ":new.txt")); Assert.Equal("one\ntwo\n", await File.ReadAllTextAsync(full));
        snapshot = await editor.ReadAsync(root, file with { Area = "staged" }, default); request = new(file with { Area = "staged" }, [Find(snapshot.Diff.Patch, "+one")], snapshot.Token); plan = await editor.PrepareAsync(root, request, default); Assert.True(plan.RemovesEntry); await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Empty(await f.RunAsync(root, "ls-files"));
        await f.RunAsync(root, "add", "--", "new.txt"); await f.RunAsync(root, "commit", "-qm", "base"); File.Delete(full);
        snapshot = await editor.ReadAsync(root, file, default); request = new(file, [Find(snapshot.Diff.Patch, "-one")], snapshot.Token); plan = await editor.PrepareAsync(root, request, default); await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Equal("two\n", await f.RunAsync(root, "show", ":new.txt")); Assert.False(File.Exists(full));
    }
    private static RepositoryLineEditor Create(string root)
    {
        var reader = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(reader);
        return new(reader, new GitBlobReader("git", TimeSpan.FromSeconds(10)), new GitMutationRunner(), inspector, new RepositoryChangesReader(reader, inspector));
    }
    [Fact]
    public async Task RealGitHonorsCleanEndOfLineConversionForANewCrLfFile()
    {
        await using var f = await GitTestDirectory.CreateAsync(); await Configure(f); var root = f.RepositoryPath;
        await f.RunAsync(root, "config", "core.autocrlf", "true");
        await File.WriteAllTextAsync(Path.Combine(root, "new.txt"), "one\r\ntwo\r\n");
        var editor = Create(root); var file = new RepositoryLinesRequest(1, "new.txt", "working"); var snapshot = await editor.ReadAsync(root, file, default);
        Assert.DoesNotContain('\r', snapshot.Diff.Patch); Assert.Empty(await f.RunAsync(root, "ls-files"));
        var request = new PrepareLinesRequest(file, [Find(snapshot.Diff.Patch, "+two")], snapshot.Token); var plan = await editor.PrepareAsync(root, request, default);
        await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Equal("two\n", await f.RunAsync(root, "show", ":new.txt")); Assert.Equal("one\r\ntwo\r\n", await File.ReadAllTextAsync(Path.Combine(root, "new.txt")));
    }
    private static int Find(string patch, string text) => Array.IndexOf(patch.Split('\n'), text);
    [Fact]
    public async Task RealGitExportsAnApplicableSelectionAndDiscardsOnlyUnstagedLines()
    {
        await using var f = await GitTestDirectory.CreateAsync(); await Configure(f); var root = f.RepositoryPath; const string path = "folder/a space ' café.cs";
        Directory.CreateDirectory(Path.Combine(root, "folder")); var full = Path.Combine(root, path); await File.WriteAllTextAsync(full, "a\nb\n"); await f.RunAsync(root, "add", "--", path); await f.RunAsync(root, "commit", "-qm", "base");
        var head = await f.RunAsync(root, "rev-parse", "HEAD"); await File.WriteAllTextAsync(full, "a\nB\nextra\n"); var editor = Create(root); var file = new RepositoryLinesRequest(1, path, "working"); var view = await editor.ReadAsync(root, file, default);
        var request = new PrepareLinesRequest(file, [Find(view.Diff.Patch, "+extra")], view.Token, ExportPatch: true); var plan = await editor.PrepareAsync(root, request, default); Assert.NotEmpty(plan.Patch); Assert.Equal("a\nb\n", await f.RunAsync(root, "show", ":" + path));
        var patchFile = Path.Combine(f.Root, "selected.patch"); await File.WriteAllTextAsync(patchFile, plan.Patch, new UTF8Encoding(false)); await f.RunAsync(root, "apply", "--cached", "--check", patchFile); await f.RunAsync(root, "apply", "--cached", patchFile);
        Assert.Equal("a\nb\nextra\n", await f.RunAsync(root, "show", ":" + path)); Assert.Equal("a\nB\nextra\n", await File.ReadAllTextAsync(full));
        view = await editor.ReadAsync(root, file, default); request = new(file, SelectedLinesBuilder.ChangedLineIds(view.Diff.Patch), view.Token, "discard"); plan = await editor.PrepareAsync(root, request, default); await editor.ExecuteAsync(root, new(request, plan.Token), default);
        Assert.Equal("a\nb\nextra\n", await File.ReadAllTextAsync(full)); Assert.Equal("a\nb\nextra\n", await f.RunAsync(root, "show", ":" + path)); Assert.Equal(head, await f.RunAsync(root, "rev-parse", "HEAD"));
    }
    private static async Task Configure(GitTestDirectory f)
    {
        foreach (var entry in new[] { ("user.name", "GitCosmos Tests"), ("user.email", "test@example.invalid"), ("commit.gpgsign", "false"), ("core.autocrlf", "false"), ("core.hooksPath", Path.Combine(f.Root, "no-hooks")) }) await f.RunAsync(f.RepositoryPath, "config", entry.Item1, entry.Item2);
    }
}
