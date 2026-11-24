using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryBranchService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryBranches branches) : IRepositoryBranchService
{
    private readonly RepositoryBranchUseCase _useCase = new(catalog, workspaces, links, branches);
    public Task<CustomResponse<LocalGitBranches>> GetAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default) => _useCase.GetAsync(request, cancellationToken);
    public Task<CustomResponse<BranchResources>> GetResourcesAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default) => _useCase.GetResourcesAsync(request, cancellationToken);
    public Task<CustomResponse<RepositoryBranchPlan>> PrepareAsync(RepositoryBranchRequest request, CancellationToken cancellationToken = default) => _useCase.PrepareAsync(request, cancellationToken);
    public Task<CustomResponse<string>> ExecuteAsync(ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken = default) => _useCase.ExecuteAsync(request, cancellationToken);
}
