using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryHistoryService
{
    Task<CustomResponse<GitHistoryPage>> GetAsync(RepositoryHistoryRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<GitCommitDetails>> GetCommitAsync(RepositoryCommitRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryCommitDiffRequest request, CancellationToken cancellationToken = default);
}
