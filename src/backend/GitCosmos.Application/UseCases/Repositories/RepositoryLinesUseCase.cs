using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.UseCases.Repositories;
public class RepositoryLinesUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryLineEditor editor)
{
    private async Task<string> RootAsync(RepositoryLinesRequest request, CancellationToken ct)
    {
        new RepositoryLinesRequestValidator().ValidateAndThrowApp(request);
        if (request.WorkspaceId == 0) return (await catalog.FindAsync(request.RepositoryId, ct) ?? throw Missing()).LocalPath;
        if (!await workspaces.ExistsAsync(request.WorkspaceId, ct)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, ct)).SingleOrDefault(repo => repo.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    public async Task<CustomResponse<RepositoryLinesSnapshot>> ReadAsync(RepositoryLinesRequest r, CancellationToken ct) => CustomResponse<RepositoryLinesSnapshot>.Ok(await editor.ReadAsync(await RootAsync(r, ct), r, ct));
    public async Task<CustomResponse<RepositoryLinesPlan>> PrepareAsync(PrepareLinesRequest r, CancellationToken ct)
    {
        new PrepareLinesRequestValidator().ValidateAndThrowApp(r);
        return CustomResponse<RepositoryLinesPlan>.Ok(await editor.PrepareAsync(await RootAsync(r.File, ct), r, ct));
    }
    public async Task<CustomResponse<bool>> ExecuteAsync(ExecuteLinesRequest r, CancellationToken ct)
    {
        if (r.Request is null || r.Token is not { Length: 64 } || !r.Token.All(Uri.IsHexDigit)) throw new ValidationAppException(LocalizedText.Get("lines.invalid"));
        new PrepareLinesRequestValidator().ValidateAndThrowApp(r.Request);
        await editor.ExecuteAsync(await RootAsync(r.Request.File, ct), r, ct); return CustomResponse<bool>.Ok(true, LocalizedText.Get(r.Request.Operation == "discard" ? "local.completed" : "lines.completed"));
    }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));
}
