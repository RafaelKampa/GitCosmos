using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Infrastructure.Git;

namespace GitCosmos.Application.Tests;

public class BranchWorkflowTests
{
    [Fact]
    public async Task RealGitWorkflowsUseOnlyTemporaryRepositoriesAndRecoverExplicitlyFromConflicts()
    {
        await using var fixture = await GitTestDirectory.CreateAsync(); var root = fixture.RepositoryPath;
        await fixture.RunAsync(root, "config", "user.name", "Tests"); await fixture.RunAsync(root, "config", "user.email", "tests@example.invalid");
        await fixture.RunAsync(root, "config", "commit.gpgSign", "false"); await fixture.RunAsync(root, "config", "tag.gpgSign", "false");
        await fixture.RunAsync(root, "config", "core.autocrlf", "false"); await fixture.RunAsync(root, "config", "core.eol", "lf");
        await fixture.RunAsync(root, "config", "core.hooksPath", Path.Combine(fixture.Root, "no-hooks"));
        File.WriteAllText(Path.Combine(root, "file.txt"), "root\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Root");
        if ((await fixture.RunAsync(root, "branch", "--show-current")).Trim() != "main") await fixture.RunAsync(root, "branch", "-m", "main");
        await fixture.RunAsync(root, "branch", "lagging"); await fixture.RunAsync(root, "switch", "-c", "feature");
        File.WriteAllText(Path.Combine(root, "feature.txt"), "feature\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Feature");
        await fixture.RunAsync(root, "switch", "main");
        var remote = Path.Combine(fixture.Root, "remote.git"); await fixture.RunAsync(fixture.Root, "init", "--bare", "--quiet", "--template=", remote);
        await fixture.RunAsync(root, "remote", "add", "origin", remote); await fixture.RunAsync(root, "push", "origin", "main", "feature"); await fixture.RunAsync(root, "fetch", "origin");
        var runner = new GitProcessRunner("git", TimeSpan.FromSeconds(10)); var inspector = new GitRepositoryInspector(runner);
        var adapter = new RepositoryBranches(runner, new GitMutationRunner(), inspector, new RepositoryChangesReader(runner, inspector));
        RepositoryBranchRequest Request(string operation, string name = "feature", bool isRemote = false, string newName = "") => new(1, 0, operation, name, newName, isRemote, "origin", "feature", Upstream: "refs/remotes/origin/feature", BaseBranch: "main");
        async Task Apply(RepositoryBranchRequest request)
        {
            var plan = await adapter.PrepareAsync(root, request);
            await adapter.ExecuteAsync(root, new(request, plan.Token));
        }
        Assert.Equal(2, (await adapter.ReadResourcesAsync(root)).Branches.Count);
        await Apply(Request("create", "origin/feature", true, "from-remote")); await Apply(Request("tag", newName: "v1")); await Apply(Request("tag", newName: "v2") with { Message = "Annotated tag" });
        await Apply(Request("setUpstream")); Assert.Equal("refs/remotes/origin/feature", (await fixture.RunAsync(root, "rev-parse", "--symbolic-full-name", "feature@{upstream}")).Trim()); await Apply(Request("unsetUpstream"));
        var worktree = Path.Combine(fixture.Root, "worktree espaço ' sample"); await Apply(Request("worktree") with { Destination = worktree }); Assert.True(File.Exists(Path.Combine(worktree, "feature.txt"))); await fixture.RunAsync(root, "worktree", "remove", worktree);
        await Apply(Request("worktree", "origin/feature", true, "worktree-remote") with { Destination = worktree }); Assert.Equal("origin/feature", (await fixture.RunAsync(worktree, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}")).Trim()); await fixture.RunAsync(root, "worktree", "remove", worktree);
        await Apply(Request("fastForward", "lagging")); await Apply(Request("fastForward", "main"));
        await Apply(Request("push", "main") with { RemoteBranch = "published" }); await Apply(Request("fetch"));
        await Apply(Request("deleteRemote", "origin/published", true) with { Confirmation = "origin/published" });
        Assert.NotEqual(0, (await runner.RunAsync(remote, ["rev-parse", "--verify", "--quiet", "refs/heads/published"])).ExitCode);
        await Apply(Request("checkoutRemote", "origin/feature", true, "tracking-local"));
        Assert.Equal("origin/feature", (await fixture.RunAsync(root, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}")).Trim()); await fixture.RunAsync(root, "switch", "main");
        await fixture.RunAsync(root, "switch", "feature"); File.WriteAllText(Path.Combine(root, "topic.txt"), "topic\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Topic change");
        await fixture.RunAsync(root, "switch", "main"); File.WriteAllText(Path.Combine(root, "current.txt"), "current\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Current change");
        var interactive = Request("interactiveRebase"); var review = await adapter.PrepareAsync(root, interactive);
        Assert.Single(review.Commits); interactive = interactive with { Steps = review.Commits.Select(commit => new RebaseStep(commit.Sha, "pick")).ToArray() }; await Apply(interactive);
        Assert.True(File.Exists(Path.Combine(root, "topic.txt"))); Assert.True(File.Exists(Path.Combine(root, "current.txt")));
        await Apply(Request("rebase")); await Apply(Request("push")); await Apply(Request("pull", "origin/feature", true));
        await fixture.RunAsync(root, "switch", "-c", "conflict-source"); File.WriteAllText(Path.Combine(root, "file.txt"), "source\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Source conflict");
        await fixture.RunAsync(root, "switch", "main"); File.WriteAllText(Path.Combine(root, "file.txt"), "current\n"); await fixture.RunAsync(root, "add", "--all"); await fixture.RunAsync(root, "commit", "-qm", "Current conflict");
        var merge = Request("merge", "conflict-source"); var mergePlan = await adapter.PrepareAsync(root, merge);
        await Assert.ThrowsAsync<OperationFailedAppException>(() => adapter.ExecuteAsync(root, new(merge, mergePlan.Token))); Assert.Equal("merge", (await adapter.ReadResourcesAsync(root)).OperationState);
        await Apply(Request("abort", "conflict-source")); Assert.Equal("current\n", File.ReadAllText(Path.Combine(root, "file.txt"))); Assert.Equal("", (await adapter.ReadResourcesAsync(root)).OperationState);
        mergePlan = await adapter.PrepareAsync(root, merge); await Assert.ThrowsAsync<OperationFailedAppException>(() => adapter.ExecuteAsync(root, new(merge, mergePlan.Token)));
        File.WriteAllText(Path.Combine(root, "file.txt"), "resolved\n"); await fixture.RunAsync(root, "add", "--", "file.txt"); await Apply(Request("continue", "conflict-source")); Assert.Equal("", (await adapter.ReadResourcesAsync(root)).OperationState);
        Assert.Equal("resolved\n", File.ReadAllText(Path.Combine(root, "file.txt")));
    }
}
