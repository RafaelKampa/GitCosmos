using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryChangesService
{
    Task<CustomResponse<RepositoryChangesResponse>> GetAsync(RepositoryChangesRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryDiffRequest request, CancellationToken cancellationToken = default);
}
