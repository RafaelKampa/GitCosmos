using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Domain.Common;

namespace GitCosmos.Application.Interfaces.Services;

public interface IWorkspaceService
{
    Task<CustomResponse<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<WorkspaceResponse>> UpdateAsync(UpdateWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<int?>> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<CustomResponse<PagedResult<WorkspaceResponse>>> GetAllAsync(PaginationRequest request, CancellationToken cancellationToken = default);
}

