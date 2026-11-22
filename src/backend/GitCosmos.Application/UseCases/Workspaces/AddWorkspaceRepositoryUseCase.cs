using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;

namespace GitCosmos.Application.UseCases.Workspaces;

public class AddWorkspaceRepositoryUseCase(IWorkspaceRepository workspaceRepository, IRepositoryLinkRepository linkRepository, IGitRepositoryInspector inspector)
{
    public async Task<CustomResponse<WorkspaceRepositoryResponse>> AddAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default)
    {
        new AddWorkspaceRepositoryRequestValidator().ValidateAndThrowApp(request);
        if (!await workspaceRepository.ExistsAsync(request.WorkspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        var location = await inspector.InspectAsync(request.LocalPath, cancellationToken);
        var result = await linkRepository.AddAsync(request.WorkspaceId,
            new GitRepository { Name = location.Name, LocalPath = location.LocalPath, PathKey = location.PathKey }, cancellationToken);
        if (result.Status == RepositoryLinkStatus.WorkspaceNotFound)
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        if (result.Status == RepositoryLinkStatus.Duplicate)
            throw new ConflictAppException(LocalizedText.Get("repository.already_linked"));
        return CustomResponse<WorkspaceRepositoryResponse>.Ok(
            WorkspaceRepositoryResponse.FromEntity(result.Repository!), LocalizedText.Get("repository.added"));
    }
}
