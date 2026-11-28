using GitCosmos.Application.DTOs.Requests.Repository;
namespace GitCosmos.Application.Interfaces.Git;
public record RepositoryLinesSnapshot(GitFileDiff Diff, string Token, bool CanSelect, string Reason);
public record RepositoryLinesPlan(string Token, string Path, string Area, int LineCount, string Result, bool RemovesEntry, string Operation = "move", string Patch = "");
public interface IRepositoryLineEditor
{
    Task<RepositoryLinesSnapshot> ReadAsync(string root, RepositoryLinesRequest request, CancellationToken ct);
    Task<RepositoryLinesPlan> PrepareAsync(string root, PrepareLinesRequest request, CancellationToken ct);
    Task ExecuteAsync(string root, ExecuteLinesRequest request, CancellationToken ct);
}
