using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Repositories;

public class RepositoryBranchUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryBranches branches)
{
    private async Task<string> FindAsync(RepositoryBranchQuery request, CancellationToken cancellationToken)
    {
        new RepositoryBranchQueryValidator().ValidateAndThrowApp(request);
        if (request.WorkspaceId == 0)
            return (await catalog.FindAsync(request.RepositoryId, cancellationToken))?.LocalPath ?? throw Missing();
        if (!await workspaces.ExistsAsync(request.WorkspaceId, cancellationToken)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, cancellationToken)).SingleOrDefault(x => x.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));
    public async Task<CustomResponse<LocalGitBranches>> GetAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default) =>
        CustomResponse<LocalGitBranches>.Ok(await branches.ReadAsync(await FindAsync(request, cancellationToken), cancellationToken));
    public async Task<CustomResponse<BranchResources>> GetResourcesAsync(RepositoryBranchQuery request, CancellationToken cancellationToken = default) =>
        CustomResponse<BranchResources>.Ok(await branches.ReadResourcesAsync(await FindAsync(request, cancellationToken), cancellationToken));
    public async Task<CustomResponse<RepositoryBranchPlan>> PrepareAsync(RepositoryBranchRequest request, CancellationToken cancellationToken = default)
    {
        new RepositoryBranchRequestValidator().ValidateAndThrowApp(request);
        return CustomResponse<RepositoryBranchPlan>.Ok(await branches.PrepareAsync(await FindAsync(new(request.RepositoryId, request.WorkspaceId), cancellationToken), request, cancellationToken));
    }
    public async Task<CustomResponse<string>> ExecuteAsync(ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Request is null || request.Token is null || request.Token.Length != 64 || !request.Token.All(Uri.IsHexDigit))
            throw new ValidationAppException(LocalizedText.Get("branch.invalid"));
        new RepositoryBranchRequestValidator().ValidateAndThrowApp(request.Request);
        var path = await FindAsync(new(request.Request.RepositoryId, request.Request.WorkspaceId), cancellationToken);
        return CustomResponse<string>.Ok(await branches.ExecuteAsync(path, request, cancellationToken), LocalizedText.Get("branch.completed"));
    }
}
