using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Application.Interfaces.Services;
public interface IRepositoryLinesService
{
    Task<CustomResponse<RepositoryLinesSnapshot>> ReadAsync(RepositoryLinesRequest request, CancellationToken ct);
    Task<CustomResponse<RepositoryLinesPlan>> PrepareAsync(PrepareLinesRequest request, CancellationToken ct);
    Task<CustomResponse<bool>> ExecuteAsync(ExecuteLinesRequest request, CancellationToken ct);
}
