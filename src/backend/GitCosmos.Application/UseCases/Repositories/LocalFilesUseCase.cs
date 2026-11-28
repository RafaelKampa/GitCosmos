using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.UseCases.Repositories;
public class LocalFilesUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, ILocalFiles files)
{
    private async Task<string> Root(LocalFilesRequest request, CancellationToken ct)
    {
        new LocalFilesRequestValidator().ValidateAndThrowApp(request);
        if (request.WorkspaceId == 0) return (await catalog.FindAsync(request.RepositoryId, ct) ?? throw Missing()).LocalPath;
        if (!await workspaces.ExistsAsync(request.WorkspaceId, ct)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, ct)).SingleOrDefault(repo => repo.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    public async Task<CustomResponse<LocalFilesView>> ReadAsync(LocalFilesRequest r, CancellationToken ct) => CustomResponse<LocalFilesView>.Ok(await files.ReadAsync(await Root(r, ct), r, ct));
    public async Task<CustomResponse<LocalFilesPlan>> PrepareAsync(LocalFilesRequest r, CancellationToken ct) => CustomResponse<LocalFilesPlan>.Ok(await files.PrepareAsync(await Root(r, ct), r, ct));
    public async Task<CustomResponse<bool>> ExecuteAsync(LocalFilesRequest r, CancellationToken ct) { await files.ExecuteAsync(await Root(r, ct), r, ct); return CustomResponse<bool>.Ok(true); }
    public async Task<CustomResponse<bool>> DesktopAsync(LocalFilesRequest r, CancellationToken ct) { await files.DesktopAsync(await Root(r, ct), r, ct); return CustomResponse<bool>.Ok(true); }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));
}
