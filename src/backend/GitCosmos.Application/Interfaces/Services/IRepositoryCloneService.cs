using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryCloneService
{
    Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(PreviewRepositoryCloneRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CloneWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default);
}
