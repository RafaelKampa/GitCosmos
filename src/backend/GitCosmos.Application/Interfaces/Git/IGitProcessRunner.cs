namespace GitCosmos.Application.Interfaces.Git;

public interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(string directory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

public record GitCommandResult(int ExitCode, string Output);
