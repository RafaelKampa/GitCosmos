using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class UpdateWorkspaceUseCase(IWorkspaceRepository workspaceRepository)
{
    public async Task<CustomResponse<WorkspaceResponse>> UpdateAsync(UpdateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        new UpdateWorkspaceRequestValidator().ValidateAndThrowApp(request);
        var workspace = await workspaceRepository.GetFullByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        var name = request.Name.Trim();
        var key = name.ToUpperInvariant();
        if (await workspaceRepository.NameExistsAsync(key, request.Id, cancellationToken))
            throw new ConflictAppException(LocalizedText.Get("workspace.name_exists"));

        workspace.Name = name;
        workspace.NameKey = key;
        workspace.RefreshUpdatedAt();
        await workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return CustomResponse<WorkspaceResponse>.Ok(WorkspaceResponse.FromEntity(workspace), LocalizedText.Get("workspace.updated"));
    }
}
