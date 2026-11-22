using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryImportService
{
    Task<CustomResponse<RepositoryImportResponse>> ImportAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default);
}
