using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryBranchService
{
    Task<CustomResponse<LocalGitBranches>> GetAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default);
    Task<CustomResponse<BranchResources>> GetResourcesAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default);
    Task<CustomResponse<RepositoryBranchPlan>> PrepareAsync(RepositoryBranchRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<string>> ExecuteAsync(ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken = default);
}
