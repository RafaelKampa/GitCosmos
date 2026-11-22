namespace GitCosmos.Application.Interfaces.Git;

public record GitFileChange(string Path, string? OriginalPath, string IndexStatus, string WorkTreeStatus,
    bool IsUntracked, bool IsConflicted, bool IsSubmodule);
public record GitChangesSnapshot(GitWorkingTreeStatus Status, IReadOnlyList<GitFileChange> Files);
public record GitFileDiff(string Path, string Area, string Kind, string Patch);

public interface IRepositoryChangesReader
{
    Task<GitChangesSnapshot> ReadAsync(string localPath, CancellationToken cancellationToken = default);
    Task<GitFileDiff> ReadDiffAsync(string localPath, string path, string area, CancellationToken cancellationToken = default);
}

public record LimitedGitOutput(int ExitCode, string Output, bool Truncated);
public interface IBoundedGitProcessRunner
{
    Task<LimitedGitOutput> RunLimitedAsync(string directory, IReadOnlyList<string> arguments, int maximumCharacters, CancellationToken cancellationToken = default);
}
