using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
namespace GitCosmos.Application.UseCases.Repositories;
public class RepositoryConflictUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryConflicts conflicts)
{
    private async Task<string> RootAsync(RepositoryConflictRequest request, CancellationToken ct)
    {
        new RepositoryConflictRequestValidator().ValidateAndThrowApp(request);
        if (request.WorkspaceId == 0) return (await catalog.FindAsync(request.RepositoryId, ct) ?? throw Missing()).LocalPath;
        if (!await workspaces.ExistsAsync(request.WorkspaceId, ct)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, ct)).SingleOrDefault(repo => repo.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    private static void Validate(ResolveConflictRequest request)
    {
        new ResolveConflictRequestValidator().ValidateAndThrowApp(request);
    }
    public async Task<CustomResponse<RepositoryConflict>> ReadAsync(RepositoryConflictRequest r, CancellationToken ct) => CustomResponse<RepositoryConflict>.Ok(await conflicts.ReadAsync(await RootAsync(r, ct), r.Path, ct));
    public async Task<CustomResponse<ConflictResolutionPlan>> PrepareAsync(ResolveConflictRequest r, CancellationToken ct)
    {
        Validate(r); return CustomResponse<ConflictResolutionPlan>.Ok(await conflicts.PrepareAsync(await RootAsync(r.File, ct), r, ct));
    }
    public async Task<CustomResponse<bool>> ResolveAsync(ResolveConflictRequest r, CancellationToken ct)
    {
        Validate(r); await conflicts.ResolveAsync(await RootAsync(r.File, ct), r, ct); return CustomResponse<bool>.Ok(true);
    }
    public async Task<CustomResponse<bool>> ExternalAsync(RepositoryConflictRequest r, CancellationToken ct)
    {
        await conflicts.ExternalAsync(await RootAsync(r, ct), r, ct); return CustomResponse<bool>.Ok(true);
    }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));
}
