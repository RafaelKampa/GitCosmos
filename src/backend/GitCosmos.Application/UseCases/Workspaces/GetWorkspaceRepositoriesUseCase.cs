using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class GetWorkspaceRepositoriesUseCase(IWorkspaceRepository workspaceRepository, IRepositoryLinkRepository linkRepository)
{
    public async Task<CustomResponse<List<WorkspaceRepositoryResponse>>> GetAllAsync(int workspaceId, CancellationToken cancellationToken = default)
    {
        new WorkspaceIdValidator().ValidateAndThrowApp(workspaceId);
        if (!await workspaceRepository.ExistsAsync(workspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        var repositories = await linkRepository.GetAllAsync(workspaceId, cancellationToken);
        return CustomResponse<List<WorkspaceRepositoryResponse>>.Ok(repositories.Select(WorkspaceRepositoryResponse.FromEntity).ToList(), repositories.Count);
    }
}
