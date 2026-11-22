using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Workspaces;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class WorkspaceDashboardService(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryStatusReader statusReader) : IWorkspaceDashboardService
{
    private readonly GetWorkspaceDashboardUseCase _useCase = new(workspaces, links, statusReader);
    public Task<CustomResponse<WorkspaceDashboardResponse>> GetAsync(int workspaceId, CancellationToken cancellationToken = default) =>
        _useCase.GetAsync(workspaceId, cancellationToken);
}
