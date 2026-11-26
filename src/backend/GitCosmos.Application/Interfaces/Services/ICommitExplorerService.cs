using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Common;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Application.Interfaces.Services;
public interface ICommitExplorerService
{
    Task<CustomResponse<IReadOnlyList<CommitTreeEntry>>> TreeAsync(CommitFileSelection request, CancellationToken cancellationToken);
    Task<CustomResponse<CommitFileContent>> FileAsync(CommitFileSelection request, CancellationToken cancellationToken);
    Task<CustomResponse<IReadOnlyList<GitCommit>>> HistoryAsync(CommitFileSelection request, CancellationToken cancellationToken);
    Task<CustomResponse<IReadOnlyList<CommitBlameLine>>> BlameAsync(CommitFileSelection request, CancellationToken cancellationToken);
    Task<CustomResponse<IReadOnlyList<CommitExternalTool>>> ToolsAsync(CommitFileSelection request, CancellationToken cancellationToken);
    Task<CustomResponse<CommitRestorePlan>> PrepareAsync(CommitRestoreRequest request, CancellationToken cancellationToken);
    Task<CustomResponse<bool>> RestoreAsync(CommitRestoreRequest request, CancellationToken cancellationToken);
    Task<CustomResponse<bool>> DesktopAsync(CommitDesktopRequest request, CancellationToken cancellationToken);
}
