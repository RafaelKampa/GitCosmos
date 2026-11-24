using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryEditService
{
    Task<CustomResponse<RepositoryEditPlan>> PrepareAsync(RepositoryEditRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<RepositoryEditResult>> ExecuteAsync(ExecuteRepositoryEditRequest request, CancellationToken cancellationToken = default);
}
