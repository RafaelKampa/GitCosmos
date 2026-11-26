using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Interfaces.Git;

public record CommitTreeEntry(string Path, string Mode, string Sha);
public record CommitFileContent(string Path, string Kind, string Text, string Base64, string FullPath);
public record CommitBlameLine(int Line, string Sha, string Author, string Time, string Text);
public record CommitRestorePlan(string Token, string Path, string SourceSha, string SourcePath, bool Exists);
public record CommitExternalTool(string Id, string Name);
public interface ICommitExplorer
{
    Task<IReadOnlyList<CommitTreeEntry>> TreeAsync(string root, string sha, CancellationToken cancellationToken);
    Task<CommitFileContent> FileAsync(string root, string sha, string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<GitCommit>> HistoryAsync(string root, string sha, string path, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommitBlameLine>> BlameAsync(string root, string sha, string path, CancellationToken cancellationToken);
    Task<CommitRestorePlan> PrepareAsync(string root, CommitRestoreRequest request, CancellationToken cancellationToken);
    Task RestoreAsync(string root, CommitRestoreRequest request, CancellationToken cancellationToken);
    Task DesktopAsync(string root, CommitDesktopRequest request, CancellationToken cancellationToken);
    IReadOnlyList<CommitExternalTool> Tools();
}
public interface IGitBlobReader
{
    Task<byte[]> ReadAsync(string root, string oid, CancellationToken cancellationToken);
}
public interface ICommitFileDesktop
{
    IReadOnlyList<CommitExternalTool> Tools();
    void Open(string name, byte[] content);
    void Diff(string tool, string name, byte[] before, byte[] after);
    void Reveal(string fullPath);
}
