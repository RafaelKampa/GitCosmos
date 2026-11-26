using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.UseCases.Repositories;

public class CommitExplorerUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, ICommitExplorer explorer)
{
    private async Task<string> RootAsync(CommitFileSelection request, bool pathRequired, CancellationToken ct)
    {
        new RepositoryCommitRequestValidator().ValidateAndThrowApp(new(request.RepositoryId, request.Sha, request.WorkspaceId));
        if (request.Path is null || request.Path.Length > 32767 || request.Path.Contains('\0') || pathRequired && request.Path.Length == 0)
            throw new ValidationAppException(LocalizedText.Get("changes.invalid_selection"));
        if (request.WorkspaceId == 0) return (await catalog.FindAsync(request.RepositoryId, ct) ?? throw Missing()).LocalPath;
        if (!await workspaces.ExistsAsync(request.WorkspaceId, ct)) throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(request.WorkspaceId, ct)).SingleOrDefault(x => x.Id == request.RepositoryId)?.LocalPath ?? throw Missing();
    }
    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));
    public async Task<CustomResponse<IReadOnlyList<CommitTreeEntry>>> TreeAsync(CommitFileSelection r, CancellationToken ct) => CustomResponse<IReadOnlyList<CommitTreeEntry>>.Ok(await explorer.TreeAsync(await RootAsync(r, false, ct), r.Sha, ct));
    public async Task<CustomResponse<CommitFileContent>> FileAsync(CommitFileSelection r, CancellationToken ct) => CustomResponse<CommitFileContent>.Ok(await explorer.FileAsync(await RootAsync(r, true, ct), r.Sha, r.Path, ct));
    public async Task<CustomResponse<IReadOnlyList<GitCommit>>> HistoryAsync(CommitFileSelection r, CancellationToken ct) => CustomResponse<IReadOnlyList<GitCommit>>.Ok(await explorer.HistoryAsync(await RootAsync(r, true, ct), r.Sha, r.Path, ct));
    public async Task<CustomResponse<IReadOnlyList<CommitBlameLine>>> BlameAsync(CommitFileSelection r, CancellationToken ct) => CustomResponse<IReadOnlyList<CommitBlameLine>>.Ok(await explorer.BlameAsync(await RootAsync(r, true, ct), r.Sha, r.Path, ct));
    public async Task<CustomResponse<IReadOnlyList<CommitExternalTool>>> ToolsAsync(CommitFileSelection r, CancellationToken ct) { await RootAsync(r, false, ct); return CustomResponse<IReadOnlyList<CommitExternalTool>>.Ok(explorer.Tools()); }
    public async Task<CustomResponse<CommitRestorePlan>> PrepareAsync(CommitRestoreRequest r, CancellationToken ct) => CustomResponse<CommitRestorePlan>.Ok(await explorer.PrepareAsync(await RootAsync(r.File, true, ct), r, ct));
    public async Task<CustomResponse<bool>> RestoreAsync(CommitRestoreRequest r, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(r.Token) || r.Token.Length != 64 || !r.Token.All(Uri.IsHexDigit)) throw new ValidationAppException(LocalizedText.Get("edit.invalid"));
        await explorer.RestoreAsync(await RootAsync(r.File, true, ct), r, ct); return CustomResponse<bool>.Ok(true);
    }
    public async Task<CustomResponse<bool>> DesktopAsync(CommitDesktopRequest r, CancellationToken ct)
    {
        if (r.Action is not ("open" or "reveal" or "diff") || r.Action == "diff" && r.Tool is not ("vscode" or "visualstudio")) throw new ValidationAppException(LocalizedText.Get("edit.invalid"));
        await explorer.DesktopAsync(await RootAsync(r.File, true, ct), r, ct); return CustomResponse<bool>.Ok(true);
    }
}
