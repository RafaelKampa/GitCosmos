using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class DeleteWorkspaceUseCase(IWorkspaceRepository workspaceRepository)
{
    public async Task<CustomResponse<int?>> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        new WorkspaceIdValidator().ValidateAndThrowApp(id);
        var workspace = await workspaceRepository.GetFullByIdAsync(id, cancellationToken)
            ?? throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        workspace.Deactivate();
        await workspaceRepository.DeleteAsync(workspace, cancellationToken);
        return CustomResponse<int?>.Ok(workspace.Id, LocalizedText.Get("workspace.deleted"));
    }
}
