using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.Workspace;

namespace GitCosmos.Application.Interfaces.Services;

public interface IWorkspaceDashboardService
{
    Task<CustomResponse<WorkspaceDashboardResponse>> GetAsync(int workspaceId, CancellationToken cancellationToken = default);
}
