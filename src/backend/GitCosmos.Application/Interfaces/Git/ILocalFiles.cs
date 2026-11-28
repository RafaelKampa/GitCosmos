using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Interfaces.Git;
public record LocalFilesView(string FullPath, string Patch, IReadOnlyList<GitCommit> History, IReadOnlyList<CommitBlameLine> Blame, IReadOnlyList<CommitExternalTool> Tools, IReadOnlyList<CommitExternalTool> Editors);
public record LocalFilesPlan(string Token, string Action, string[] Paths, string Preview);
public interface ILocalFiles
{
    Task<LocalFilesView> ReadAsync(string root, LocalFilesRequest request, CancellationToken ct);
    Task<LocalFilesPlan> PrepareAsync(string root, LocalFilesRequest request, CancellationToken ct);
    Task ExecuteAsync(string root, LocalFilesRequest request, CancellationToken ct);
    Task DesktopAsync(string root, LocalFilesRequest request, CancellationToken ct);
}
public interface IWorkingFileEditors
{
    IReadOnlyList<CommitExternalTool> Tools(string path);
    void OpenWith(string path);
    void RevealLine(string path, string tool, int line);
}
