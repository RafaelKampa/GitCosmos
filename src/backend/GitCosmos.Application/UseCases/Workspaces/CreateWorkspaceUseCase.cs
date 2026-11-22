using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class CreateWorkspaceUseCase(IWorkspaceRepository workspaceRepository)
{
    public async Task<CustomResponse<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        new CreateWorkspaceRequestValidator().ValidateAndThrowApp(request);
        var name = request.Name.Trim();
        var key = name.ToUpperInvariant();
        if (await workspaceRepository.NameExistsAsync(key, cancellationToken))
            throw new ConflictAppException(LocalizedText.Get("workspace.name_exists"));

        var workspace = new Workspace { Name = name, NameKey = key };
        workspace.Id = await workspaceRepository.CreateAsync(workspace, cancellationToken);
        return CustomResponse<WorkspaceResponse>.Ok(WorkspaceResponse.FromEntity(workspace), LocalizedText.Get("workspace.created"));
    }
}

