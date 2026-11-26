using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Common;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.Services;
public class CommitExplorerService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, ICommitExplorer explorer) : ICommitExplorerService
{
    private readonly CommitExplorerUseCase _useCase = new(catalog, workspaces, links, explorer);
    public Task<CustomResponse<IReadOnlyList<CommitTreeEntry>>> TreeAsync(CommitFileSelection r, CancellationToken ct) => _useCase.TreeAsync(r, ct);
    public Task<CustomResponse<CommitFileContent>> FileAsync(CommitFileSelection r, CancellationToken ct) => _useCase.FileAsync(r, ct);
    public Task<CustomResponse<IReadOnlyList<GitCommit>>> HistoryAsync(CommitFileSelection r, CancellationToken ct) => _useCase.HistoryAsync(r, ct);
    public Task<CustomResponse<IReadOnlyList<CommitBlameLine>>> BlameAsync(CommitFileSelection r, CancellationToken ct) => _useCase.BlameAsync(r, ct);
    public Task<CustomResponse<IReadOnlyList<CommitExternalTool>>> ToolsAsync(CommitFileSelection r, CancellationToken ct) => _useCase.ToolsAsync(r, ct);
    public Task<CustomResponse<CommitRestorePlan>> PrepareAsync(CommitRestoreRequest r, CancellationToken ct) => _useCase.PrepareAsync(r, ct);
    public Task<CustomResponse<bool>> RestoreAsync(CommitRestoreRequest r, CancellationToken ct) => _useCase.RestoreAsync(r, ct);
    public Task<CustomResponse<bool>> DesktopAsync(CommitDesktopRequest r, CancellationToken ct) => _useCase.DesktopAsync(r, ct);
}
