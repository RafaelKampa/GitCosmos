using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Repositories;

public class GetRepositoryChangesUseCase(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryChangesReader reader)
{
    private async Task<GitRepository> FindAsync(RepositoryChangesRequest request, CancellationToken cancellationToken)
    {
        new RepositoryChangesRequestValidator().ValidateAndThrowApp(request);
        if (!await workspaces.ExistsAsync(request.WorkspaceId, cancellationToken)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, cancellationToken)).SingleOrDefault(x => x.Id == request.RepositoryId)
            ?? throw new NotFoundAppException(LocalizedText.Get("repository.link_not_found"));
    }

    public async Task<CustomResponse<RepositoryChangesResponse>> GetAsync(RepositoryChangesRequest request, CancellationToken cancellationToken = default)
    {
        var repository = await FindAsync(request, cancellationToken);
        var snapshot = await reader.ReadAsync(repository.LocalPath, cancellationToken);
        return CustomResponse<RepositoryChangesResponse>.Ok(new(repository.Id, repository.Name, repository.LocalPath, snapshot.Status, snapshot.Files));
    }

    public async Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryDiffRequest request, CancellationToken cancellationToken = default)
    {
        var repository = await FindAsync(new(request.WorkspaceId, request.RepositoryId), cancellationToken);
        new RepositoryDiffRequestValidator().ValidateAndThrowApp(request);
        return CustomResponse<GitFileDiff>.Ok(await reader.ReadDiffAsync(repository.LocalPath, request.Path, request.Area, cancellationToken));
    }
}
