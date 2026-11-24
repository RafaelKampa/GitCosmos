using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Repositories;

public class RepositoryHistoryUseCase(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryHistoryReader reader)
{
    private async Task<GitRepository> FindAsync(int repositoryId, int workspaceId, CancellationToken cancellationToken)
    {
        if (workspaceId == 0)
            return await catalog.FindAsync(repositoryId, cancellationToken) ?? throw Missing();
        if (!await workspaces.ExistsAsync(workspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        return (await links.GetAllAsync(workspaceId, cancellationToken)).SingleOrDefault(x => x.Id == repositoryId) ?? throw Missing();
    }

    private static NotFoundAppException Missing() => new(LocalizedText.Get("repository.link_not_found"));

    public async Task<CustomResponse<GitHistoryPage>> GetAsync(RepositoryHistoryRequest request, CancellationToken cancellationToken = default)
    {
        new RepositoryHistoryRequestValidator().ValidateAndThrowApp(request);
        var repository = await FindAsync(request.RepositoryId, request.WorkspaceId, cancellationToken);
        return CustomResponse<GitHistoryPage>.Ok(string.IsNullOrEmpty(request.Branch) && string.IsNullOrWhiteSpace(request.Search)
            ? await reader.ReadAsync(repository.LocalPath, request.Page, request.PageSize, request.Snapshot, cancellationToken)
            : await reader.ReadFilteredAsync(repository.LocalPath, request.Page, request.PageSize, request.Snapshot, request.Branch, request.Search, cancellationToken));
    }

    public async Task<CustomResponse<GitCommitDetails>> GetCommitAsync(RepositoryCommitRequest request, CancellationToken cancellationToken = default)
    {
        new RepositoryCommitRequestValidator().ValidateAndThrowApp(request);
        var repository = await FindAsync(request.RepositoryId, request.WorkspaceId, cancellationToken);
        return CustomResponse<GitCommitDetails>.Ok(await reader.ReadCommitAsync(repository.LocalPath, request.Sha, cancellationToken));
    }

    public async Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryCommitDiffRequest request, CancellationToken cancellationToken = default)
    {
        new RepositoryCommitRequestValidator().ValidateAndThrowApp(new(request.RepositoryId, request.Sha, request.WorkspaceId));
        if (string.IsNullOrEmpty(request.Path) || request.Path.Length > 32767 || request.Path.Contains('\0'))
            throw new ValidationAppException(LocalizedText.Get("changes.invalid_selection"));
        var repository = await FindAsync(request.RepositoryId, request.WorkspaceId, cancellationToken);
        return CustomResponse<GitFileDiff>.Ok(await reader.ReadDiffAsync(repository.LocalPath, request.Sha, request.Path, cancellationToken));
    }
}
