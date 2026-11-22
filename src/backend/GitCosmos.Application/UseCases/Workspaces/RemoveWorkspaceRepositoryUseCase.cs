using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class RemoveWorkspaceRepositoryUseCase(IWorkspaceRepository workspaceRepository, IRepositoryLinkRepository linkRepository)
{
    public async Task<CustomResponse<int?>> RemoveAsync(int workspaceId, int repositoryId, CancellationToken cancellationToken = default)
    {
        new WorkspaceIdValidator().ValidateAndThrowApp(workspaceId);
        if (repositoryId <= 0)
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_id"));
        if (!await workspaceRepository.ExistsAsync(workspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        if (!await linkRepository.RemoveAsync(workspaceId, repositoryId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("repository.link_not_found"));
        return CustomResponse<int?>.Ok(repositoryId, LocalizedText.Get("repository.removed"));
    }
}
