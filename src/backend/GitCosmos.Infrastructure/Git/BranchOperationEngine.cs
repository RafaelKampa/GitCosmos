using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class BranchOperationEngine(IBoundedGitProcessRunner reader, IGitMutationRunner writer)
{
    public static bool Handles(RepositoryBranchRequest request) => request.IsRemote || request.Operation is not ("create" or "rename" or "delete" or "checkout");
    private async Task<string> ReadAsync(string path, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var result = await reader.RunLimitedAsync(path, arguments, 1_000_000, token);
        if (result.ExitCode != 0 || result.Truncated) throw Failed();
        return result.Output;
    }
    private async Task ValidateRefAsync(string path, string reference, CancellationToken token)
    {
        var result = await reader.RunLimitedAsync(path, ["check-ref-format", reference], 256, token);
        if (result.Truncated || result.ExitCode is not (0 or 1)) throw Failed();
        if (result.ExitCode == 1) throw Invalid();
    }
    private async Task<string> ShaAsync(string path, string reference, CancellationToken token)
    {
        var value = (await ReadAsync(path, ["rev-parse", "--verify", "--end-of-options", reference + "^{commit}"], token)).Trim();
        if (!GitHistoryParser.IsSha(value)) throw Failed();
        return value;
    }
    public async Task<BranchResources> ResourcesAsync(string path, CancellationToken token)
    {
        var remotes = (await ReadAsync(path, ["remote"], token)).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(value => value.TrimEnd('\r')).ToArray();
        if (remotes.Any(value => value.StartsWith('-') || value.Any(char.IsControl))) throw Failed();
        var output = await ReadAsync(path, ["for-each-ref", "--sort=refname", "--format=%(refname)%00%(objectname)%00%(symref)", "refs/remotes/"], token);
        var branches = new List<RemoteGitBranch>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\0');
            if (fields.Length != 3 || !fields[0].StartsWith("refs/remotes/", StringComparison.Ordinal) || !GitHistoryParser.IsSha(fields[1])) throw Failed();
            if (fields[2].Length > 0) continue;
            var name = fields[0][13..];
            var remote = remotes.OrderByDescending(value => value.Length).FirstOrDefault(value => name.StartsWith(value + "/", StringComparison.Ordinal)) ?? name.Split('/')[0];
            if (name.Length <= remote.Length + 1) throw Failed();
            branches.Add(new(name, fields[1], remote, name[(remote.Length + 1)..]));
        }
        var symbolic = await reader.RunLimitedAsync(path, ["symbolic-ref", "--quiet", "--short", "HEAD"], 32768, token);
        if (symbolic.Truncated || symbolic.ExitCode is not (0 or 1)) throw Failed();
        var gitDir = (await ReadAsync(path, ["rev-parse", "--absolute-git-dir"], token)).Trim();
        var state = Path.Exists(Path.Combine(gitDir, "MERGE_HEAD")) ? "merge" : new[] { "rebase-merge", "rebase-apply" }.Any(name => Path.Exists(Path.Combine(gitDir, name))) ? "rebase"
            : new[] { "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG" }.Any(name => Path.Exists(Path.Combine(gitDir, name))) ? "other" : "";
        var currentBranch = symbolic.Output.Trim();
        if (currentBranch.Length == 0 && state == "rebase")
        {
            foreach (var directory in new[] { "rebase-merge", "rebase-apply" })
            {
                var file = new FileInfo(Path.Combine(gitDir, directory, "head-name"));
                if (!file.Exists) continue;
                if (file.Length > 32768 || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Failed();
                var reference = (await File.ReadAllTextAsync(file.FullName, token)).Trim();
                if (reference.StartsWith("refs/heads/", StringComparison.Ordinal)) currentBranch = reference[11..];
            }
        }
        return new(currentBranch, state, remotes, branches);
    }
    public async Task<RepositoryBranchPlan> PrepareAsync(string path, LocalGitBranches local, RepositoryBranchRequest request, CancellationToken token)
    {
        var resources = await ResourcesAsync(path, token);
        var remoteSource = request.IsRemote ? resources.Branches.FirstOrDefault(branch => branch.Name == request.Name) ?? throw Missing() : null;
        var source = request.IsRemote ? new LocalGitBranch(remoteSource!.Name, remoteSource.Sha, null, false, false)
            : local.Branches.FirstOrDefault(branch => branch.Name == request.Name) ?? (request.Operation == "fetch" ? new LocalGitBranch(request.Name, "", null, false, false) : throw Missing());
        var recovery = request.Operation is "continue" or "abort";
        if (recovery ? resources.OperationState is not ("merge" or "rebase") : resources.OperationState.Length > 0) throw new ConflictAppException(LocalizedText.Get("edit.in_progress"));
        var status = await ReadAsync(path, ["status", "--porcelain=v2", "-z", "--untracked-files=all"], token);
        var head = local.HasCommits ? await ShaAsync(path, "HEAD", token) : "";
        var cleanOperation = request.Operation is "merge" or "rebase" or "interactiveRebase" or "pull" or "checkoutRemote" || request.Operation == "fastForward" && source.IsCurrent;
        if (cleanOperation && (status.Length > 0 || request.Operation != "checkoutRemote" && (local.IsDetached || head.Length == 0))) throw new ConflictAppException(LocalizedText.Get("branch.checkout_blocked"));
        if (request.IsRemote && request.Operation is "rename" or "delete" or "checkout" or "push" or "fastForward" or "setUpstream" or "unsetUpstream") throw Invalid();
        if (request.Operation is "rename" or "delete" || request.Operation == "fastForward" && source.IsCheckedOut && !source.IsCurrent) throw new ConflictAppException(LocalizedText.Get("branch.checked_out"));
        var target = ""; var targetSha = ""; var urls = ""; string? url = null; var published = false;
        IReadOnlyList<RebaseCommit> commits = [];
        if (request.Operation is "create" or "checkoutRemote" || request.Operation == "worktree" && request.IsRemote)
        {
            await ValidateRefAsync(path, "refs/heads/" + request.NewName, token);
            var existing = local.Branches.FirstOrDefault(branch => branch.Name.Equals(request.NewName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (request.Operation != "checkoutRemote" || existing.Name != request.NewName || existing.Upstream != "refs/remotes/" + request.Name) throw new ConflictAppException(LocalizedText.Get("branch.exists"));
                if (existing.IsCheckedOut || existing.IsCurrent) throw new ConflictAppException(LocalizedText.Get("branch.checked_out"));
                target = existing.Name; targetSha = existing.Sha;
            }
        }
        if (request.Operation == "checkoutRemote" && !request.IsRemote) throw Invalid();
        if (request.Operation == "worktree")
        {
            if (!request.IsRemote && source.IsCheckedOut) throw new ConflictAppException(LocalizedText.Get("branch.checked_out"));
            target = ValidateDestination(path, request.Destination);
        }
        if (request.Operation == "tag")
        {
            await ValidateRefAsync(path, "refs/tags/" + request.NewName, token);
            var exists = await reader.RunLimitedAsync(path, ["show-ref", "--verify", "--quiet", "refs/tags/" + request.NewName], 256, token);
            if (exists.Truncated || exists.ExitCode is not (0 or 1)) throw Failed();
            if (exists.ExitCode == 0) throw new ConflictAppException(LocalizedText.Get("branch.exists"));
        }
        if (request.Operation is "fastForward" or "setUpstream")
        {
            var upstream = resources.Branches.FirstOrDefault(branch => "refs/remotes/" + branch.Name == request.Upstream) ?? throw Invalid();
            target = request.Upstream; targetSha = upstream.Sha;
            if (request.Operation == "fastForward")
            {
                var merged = await reader.RunLimitedAsync(path, ["merge-base", "--is-ancestor", source.Sha, targetSha], 256, token);
                if (merged.Truncated || merged.ExitCode is not (0 or 1)) throw Failed();
                if (merged.ExitCode == 1) throw new ConflictAppException(LocalizedText.Get("branch.not_ff"));
            }
        }
        if (request.Operation == "unsetUpstream" && source.Upstream is null) throw Invalid();
        if (request.Operation is "merge" or "rebase" or "interactiveRebase")
        {
            if (source.IsCurrent) throw Invalid();
            target = resources.CurrentBranch;
            await ReadAsync(path, ["var", "GIT_COMMITTER_IDENT"], token);
            await ReadAsync(path, ["var", "GIT_AUTHOR_IDENT"], token);
            if (request.Operation is "rebase" or "interactiveRebase")
                published = (await ReadAsync(path, ["for-each-ref", "--contains=" + head, "--format=%(refname)", "refs/remotes/"], token)).Trim().Length > 0;
            if (request.Operation == "interactiveRebase")
            {
                var range = GitHistoryParser.ParseCommits(await ReadAsync(path, ["log", "--reverse", "--topo-order", "--no-show-signature", "--no-notes", "--encoding=UTF-8", "-z", "--format=" + GitHistoryParser.Format, "--max-count=201", source.Sha + ".." + head, "--"], token));
                if (range.Count == 0 || range.Count > 200 || range.Any(commit => commit.Parents.Count > 1)) throw new ValidationAppException(LocalizedText.Get("branch.rebase_range"));
                commits = range.Select(commit => new RebaseCommit(commit.Sha, commit.Subject)).ToArray();
                if (request.Steps is not null && (request.Steps.Length != commits.Count || request.Steps.Select(step => step.Sha).Distinct().Count() != commits.Count || request.Steps.Any(step => !commits.Any(commit => commit.Sha == step.Sha)) || request.Steps.All(step => step.Action == "drop") || request.Steps.First(step => step.Action != "drop").Action is "squash" or "fixup")) throw Invalid();
            }
        }
        if (request.Operation is "push" or "pullRequest" or "deleteRemote" or "pull" or "fetch")
        {
            var remote = remoteSource?.Remote ?? request.Remote;
            if (!resources.Remotes.Contains(remote, StringComparer.Ordinal)) throw Invalid();
            await ValidateRefAsync(path, "refs/remotes/" + remote + "/gitcosmos-validation", token);
            target = remote;
            urls = await ReadAsync(path, ["remote", "get-url", .. (request.Operation is "push" or "deleteRemote" ? new[] { "--push" } : Array.Empty<string>()), "--all", remote], token);
            if (urls.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length != 1) throw Invalid();
            if (request.Operation is "push" or "pullRequest") await ValidateRefAsync(path, "refs/heads/" + (remoteSource?.Branch ?? request.RemoteBranch), token);
            if (request.Operation == "pull" && remoteSource is null) throw Invalid();
            if (request.Operation == "deleteRemote" && (remoteSource is null || request.Confirmation != request.Name)) throw Invalid();
            if (request.Operation == "pullRequest")
            {
                var sourceBranch = remoteSource?.Branch ?? request.RemoteBranch;
                if (!resources.Branches.Any(branch => branch.Remote == remote && branch.Branch == sourceBranch && (request.IsRemote || branch.Sha == source.Sha))) throw new ValidationAppException(LocalizedText.Get("branch.publish_first"));
                await ValidateRefAsync(path, "refs/heads/" + request.BaseBranch, token);
                url = PullRequestLinks.Create(urls.Trim(), sourceBranch, request.BaseBranch);
            }
        }
        var refs = await ReadAsync(path, ["for-each-ref", "--format=%(refname)%00%(objectname)%00%(upstream)"], token);
        var index = await ReadAsync(path, ["ls-files", "--stage", "-z"], token);
        string Fingerprint(RepositoryBranchRequest value) => JsonSerializer.Serialize(new { Path = path, Request = value, local, resources, head, status, refs, index, target, targetSha, urls, commits });
        var fingerprint = Fingerprint(request);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));
        var stateHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Fingerprint(request with { Steps = null }))));
        return new(hash, request.Operation, request.Name, request.NewName, source.Sha) { CurrentBranch = resources.CurrentBranch, Head = head, Target = target, TargetSha = targetSha, StateToken = stateHash, Url = url, RewritesHistory = request.Operation is "rebase" or "interactiveRebase", Published = published, Commits = commits };
    }
    public async Task<string> ExecuteAsync(string path, RepositoryBranchRequest request, RepositoryBranchPlan plan, CancellationToken token)
    {
        var remoteBranch = request.IsRemote && request.Operation is "push" or "pullRequest" or "pull" or "deleteRemote" ? request.Name[(plan.Target.Length + 1)..] : request.RemoteBranch;
        List<string> args = request.Operation switch
        {
            "create" => ["branch", "--no-track", "--no-recurse-submodules", "--", request.NewName, plan.Sha],
            "checkoutRemote" when plan.Target.Length > 0 => ["switch", "--no-guess", "--no-overwrite-ignore", "--no-recurse-submodules", "--", plan.Target],
            "checkoutRemote" => ["switch", "--no-guess", "--no-overwrite-ignore", "--no-recurse-submodules", "--track", "-c", request.NewName, "--", "refs/remotes/" + request.Name],
            "worktree" when request.IsRemote => ["worktree", "add", "--track", "-b", request.NewName, "--", plan.Target, "refs/remotes/" + request.Name],
            "worktree" => ["worktree", "add", "--", plan.Target, request.Name],
            "fastForward" when plan.CurrentBranch == request.Name => ["merge", "--ff-only", "--no-edit", "--no-autostash", "--no-overwrite-ignore", plan.TargetSha],
            "fastForward" => ["update-ref", "-m", "GitCosmos fast-forward", "refs/heads/" + request.Name, plan.TargetSha, plan.Sha],
            "merge" => ["merge", "--ff", "--no-edit", "--no-autostash", "--no-overwrite-ignore", plan.Sha],
            "rebase" => ["-c", "core.editor=true", "rebase", "--no-autostash", "--no-update-refs", "--no-rebase-merges", plan.Sha],
            "tag" when request.Message.Length > 0 => ["tag", "-a", "-m", request.Message, "--", request.NewName, plan.Sha],
            "tag" => ["tag", "--", request.NewName, plan.Sha],
            "setUpstream" => ["branch", "--set-upstream-to=" + plan.Target, "--", request.Name],
            "unsetUpstream" => ["branch", "--unset-upstream", "--", request.Name],
            "push" => ["push", "--no-force", "--no-mirror", "--no-follow-tags", "--recurse-submodules=no", "--", plan.Target, plan.Sha + ":refs/heads/" + remoteBranch],
            // The lease is only a compare-and-swap guard on the explicitly reviewed remote deletion.
            "deleteRemote" => ["push", "--no-mirror", "--no-follow-tags", "--recurse-submodules=no", "--force-with-lease=refs/heads/" + remoteBranch + ":" + plan.Sha, "--", plan.Target, ":refs/heads/" + remoteBranch],
            "pull" => ["pull", "--ff-only", "--no-rebase", "--no-autostash", "--no-recurse-submodules", "--", plan.Target, "refs/heads/" + remoteBranch],
            "fetch" => ["fetch", "--no-tags", "--no-recurse-submodules", "--", plan.Target, "refs/heads/*:refs/remotes/" + plan.Target + "/*"],
            _ => []
        };
        if (request.Operation == "pullRequest") return plan.Url ?? throw Invalid();
        if (request.Operation == "pull")
        {
            var fetched = await writer.RunAsync(path, ["fetch", "--no-tags", "--no-recurse-submodules", "--", plan.Target, "refs/heads/" + remoteBranch], token);
            if (fetched.ExitCode != 0) throw Failed();
            var fetchedSha = await ShaAsync(path, "FETCH_HEAD", token);
            args = ["merge", "--ff-only", "--no-edit", "--no-autostash", "--no-overwrite-ignore", fetchedSha];
        }
        if (request.Operation is "continue" or "abort")
        {
            var state = (await ResourcesAsync(path, token)).OperationState;
            if (state is not ("merge" or "rebase")) throw Invalid();
            args = ["-c", "core.editor=true", state, request.Operation == "continue" ? "--continue" : "--abort"];
        }
        if (request.Operation == "interactiveRebase")
        {
            if (request.Steps is null) throw Invalid();
            return await InteractiveRebase.ExecuteAsync(path, plan.Sha, request.Steps, writer, token);
        }
        if (args.Count == 0) throw Invalid();
        var result = await writer.RunAsync(path, args, token);
        if (result.ExitCode != 0) throw Failed();
        return request.Operation == "worktree" ? plan.Target : request.Name;
    }
    public static string ValidateDestination(string root, string destination)
    {
        if (!Path.IsPathFullyQualified(destination)) throw Invalid();
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var original = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Path.Exists(full) || full.Equals(original, StringComparison.OrdinalIgnoreCase) || full.StartsWith(original + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        var parent = Directory.GetParent(full);
        if (parent is null || !parent.Exists) throw Invalid();
        for (var current = parent; current is not null; current = current.Parent)
            if (current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Invalid();
        return full;
    }
    private static ValidationAppException Invalid() => new(LocalizedText.Get("branch.invalid"));
    private static NotFoundAppException Missing() => new(LocalizedText.Get("branch.missing"));
    private static OperationFailedAppException Failed() => new(LocalizedText.Get("branch.operation_failed"));
}
