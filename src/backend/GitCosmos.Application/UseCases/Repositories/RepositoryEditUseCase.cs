using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Repositories;

public class RepositoryEditUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryEditor editor)
{
    private async Task<string> FindAsync(RepositoryEditRequest request, CancellationToken cancellationToken)
    {
        new RepositoryEditRequestValidator().ValidateAndThrowApp(request);
        if (request.WorkspaceId == 0)
            return (await catalog.FindAsync(request.RepositoryId, cancellationToken))?.LocalPath ?? throw Missing();
        if (!await workspaces.ExistsAsync(request.WorkspaceId, cancellationToken)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, cancellationToken)).SingleOrDefault(x => x.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));

    public async Task<CustomResponse<RepositoryEditPlan>> PrepareAsync(RepositoryEditRequest request, CancellationToken cancellationToken = default) =>
        CustomResponse<RepositoryEditPlan>.Ok(await editor.PrepareAsync(await FindAsync(request, cancellationToken), request, cancellationToken));

    public async Task<CustomResponse<RepositoryEditResult>> ExecuteAsync(ExecuteRepositoryEditRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Request is null || request.Token is null || request.Token.Length != 64 || !request.Token.All(Uri.IsHexDigit))
            throw new ValidationAppException(LocalizedText.Get("edit.invalid"));
        var result = await editor.ExecuteAsync(await FindAsync(request.Request, cancellationToken), request, cancellationToken);
        return CustomResponse<RepositoryEditResult>.Ok(result, LocalizedText.Get(result.Warning is null ? "edit.completed" : "edit.push_failed"));
    }
}
