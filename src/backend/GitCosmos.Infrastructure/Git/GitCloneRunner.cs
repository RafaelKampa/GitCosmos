using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public class GitCloneRunner : IGitCloneRunner
{
    private readonly GitProcessRunner _runner = new("git", Timeout.InfiniteTimeSpan);
    private readonly SemaphoreSlim _slots = new(2, 2);
    public async Task<GitCommandResult> CloneAsync(string remoteUrl, string destinationPath, CancellationToken cancellationToken = default)
    {
        await _slots.WaitAsync(cancellationToken);
        try
        {
            return await _runner.RunAsync(Path.GetDirectoryName(destinationPath)!, ["clone", "--", remoteUrl, destinationPath], cancellationToken);
        }
        finally { _slots.Release(); }
    }
}
