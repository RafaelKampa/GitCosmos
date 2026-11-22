using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;

namespace GitCosmos.Application.Interfaces.Services;

public interface IWorkspaceRepositoryService
{
    Task<CustomResponse<WorkspaceRepositoryResponse>> AddAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<List<WorkspaceRepositoryResponse>>> GetAllAsync(int workspaceId, CancellationToken cancellationToken = default);
    Task<CustomResponse<int?>> RemoveAsync(int workspaceId, int repositoryId, CancellationToken cancellationToken = default);
}
