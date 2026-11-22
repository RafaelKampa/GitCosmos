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

public class CloneWorkspaceRepositoryUseCase(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryCloner cloner)
{
    public async Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(PreviewRepositoryCloneRequest request, CancellationToken cancellationToken = default)
    {
        new PreviewRepositoryCloneRequestValidator().ValidateAndThrowApp(request);
        if (!await workspaces.ExistsAsync(request.WorkspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return CustomResponse<RepositoryClonePlan>.Ok(cloner.Plan(request.RemoteUrl, request.ParentPath));
    }

    public async Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CloneWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(new(request.WorkspaceId, request.RemoteUrl, request.ParentPath), cancellationToken);
        var plan = preview.Result!;
        if (!string.Equals(request.DestinationPath, plan.DestinationPath, StringComparison.OrdinalIgnoreCase))
            throw new ValidationAppException(LocalizedText.Get("clone.destination_changed"));
        var repository = await cloner.CloneAsync(plan, cancellationToken);
        try
        {
            var result = await links.AddAsync(request.WorkspaceId,
                new GitRepository { Name = repository.Name, LocalPath = repository.LocalPath, PathKey = repository.PathKey }, cancellationToken);
            if (result.Status == RepositoryLinkStatus.WorkspaceNotFound)
                throw new ConflictAppException(LocalizedText.Get("clone.link_failed", repository.LocalPath));
            return CustomResponse<WorkspaceRepositoryResponse>.Ok(WorkspaceRepositoryResponse.FromEntity(result.Repository!),
                LocalizedText.Get("clone.completed", repository.LocalPath));
        }
        catch (OperationCanceledException) { throw; }
        catch (AppException) { throw new ConflictAppException(LocalizedText.Get("clone.link_failed", repository.LocalPath)); }
        catch (Exception) { throw new OperationFailedAppException(LocalizedText.Get("clone.link_failed", repository.LocalPath)); }
    }
}
