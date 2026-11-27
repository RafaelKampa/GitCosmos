using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Application.Interfaces.Services;
public interface IRepositoryConflictService
{
    Task<CustomResponse<RepositoryConflict>> ReadAsync(RepositoryConflictRequest request, CancellationToken cancellationToken);
    Task<CustomResponse<ConflictResolutionPlan>> PrepareAsync(ResolveConflictRequest request, CancellationToken cancellationToken);
    Task<CustomResponse<bool>> ResolveAsync(ResolveConflictRequest request, CancellationToken cancellationToken);
    Task<CustomResponse<bool>> ExternalAsync(RepositoryConflictRequest request, CancellationToken cancellationToken);
}
