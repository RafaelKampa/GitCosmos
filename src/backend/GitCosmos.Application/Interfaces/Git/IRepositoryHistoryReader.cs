namespace GitCosmos.Application.Interfaces.Git;

public record GitCommit(string Sha, IReadOnlyList<string> Parents, string Author, string Email,
    string AuthoredAt, string Subject, string Refs, string Message);
public record GitHistoryPage(IReadOnlyList<GitCommit> Commits, string Snapshot, bool HasMore);
public record GitCommitFile(string Path, string? OriginalPath, string Status);
public record GitCommitDetails(GitCommit Commit, IReadOnlyList<GitCommitFile> Files);

public interface IRepositoryHistoryReader
{
    Task<GitHistoryPage> ReadAsync(string localPath, int page, int pageSize, string? snapshot, CancellationToken cancellationToken = default);
    Task<GitCommitDetails> ReadCommitAsync(string localPath, string sha, CancellationToken cancellationToken = default);
    Task<GitFileDiff> ReadDiffAsync(string localPath, string sha, string path, CancellationToken cancellationToken = default);
}
