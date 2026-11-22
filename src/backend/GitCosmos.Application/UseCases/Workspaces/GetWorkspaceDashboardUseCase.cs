using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class GetWorkspaceDashboardUseCase(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryStatusReader statusReader)
{
    public async Task<CustomResponse<WorkspaceDashboardResponse>> GetAsync(int workspaceId, CancellationToken cancellationToken = default)
    {
        new WorkspaceIdValidator().ValidateAndThrowApp(workspaceId);
        var workspace = await workspaces.GetFullByIdAsync(workspaceId, cancellationToken)
            ?? throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        var repositories = await links.GetAllAsync(workspaceId, cancellationToken);
        var items = new List<RepositoryDashboardResponse>();
        // Sequential reads bound resource use and never share a DbContext across concurrent tasks.
        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var status = await statusReader.ReadAsync(repository.LocalPath, cancellationToken);
                var state = status.Conflicted > 0 ? "conflicted" : status.ChangedFiles > 0 ? "modified" : "clean";
                items.Add(new(repository.Id, repository.Name, repository.LocalPath, state, status, null));
            }
            catch (AppException exception)
            {
                items.Add(new(repository.Id, repository.Name, repository.LocalPath, "unavailable", null, exception.Message));
            }
        }
        return CustomResponse<WorkspaceDashboardResponse>.Ok(new(workspace.Id, workspace.Name, DateTimeOffset.UtcNow, items));
    }
}
