namespace GitCosmos.Application.Interfaces.Git;

public interface IRepositoryStatusReader
{
    Task<GitWorkingTreeStatus> ReadAsync(string localPath, CancellationToken cancellationToken = default);
}

public record GitWorkingTreeStatus(string Branch, bool IsDetached, bool HasCommits,
    int ChangedFiles, int Modified, int Added, int Deleted, int Renamed, int Untracked, int Conflicted, int Staged, int Unstaged)
{
    public string? Upstream { get; init; }
    public int? Ahead { get; init; }
    public int? Behind { get; init; }
}
