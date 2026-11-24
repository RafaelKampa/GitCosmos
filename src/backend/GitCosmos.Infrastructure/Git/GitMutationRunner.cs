using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class GitMutationRunner : IGitMutationRunner
{
    private readonly GitProcessRunner _runner = new("git", TimeSpan.FromMinutes(2));
    public Task<LimitedGitOutput> RunAsync(string localPath, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
        _runner.RunLimitedAsync(localPath, arguments, 200_000, cancellationToken);
}
