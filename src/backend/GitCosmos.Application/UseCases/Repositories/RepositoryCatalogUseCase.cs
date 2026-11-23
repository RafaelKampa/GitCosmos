using FluentValidation;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Repository;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Repositories;

public class RepositoryCatalogUseCase(IRepositoryCatalogRepository catalog, IRepositoryStatusReader statusReader,
    IRepositoryChangesReader changes, IGitRepositoryInspector inspector, IChildFolderReader folders, IRepositoryCloner cloner)
{
    public async Task<CustomResponse<RepositoryCatalogResponse>> GetAsync(bool includeStatus, CancellationToken cancellationToken = default)
    {
        var snapshot = await catalog.GetAsync(cancellationToken);
        var items = new List<CatalogRepository>();
        foreach (var repository in snapshot.Repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new CatalogRepository(repository.Id, repository.Name, repository.LocalPath, repository.Workspaces.Select(x => x.WorkspaceId).ToArray());
            if (includeStatus)
            {
                try
                {
                    var status = await statusReader.ReadAsync(repository.LocalPath, cancellationToken);
                    item = item with { Status = status, State = status.Conflicted > 0 ? "conflicted" : status.ChangedFiles > 0 ? "modified" : "clean" };
                }
                catch (AppException exception) { item = item with { State = "unavailable", Error = exception.Message }; }
            }
            items.Add(item);
        }
        return CustomResponse<RepositoryCatalogResponse>.Ok(new(snapshot.Workspaces.Select(x => new CatalogWorkspace(x.Id, x.Name)).ToArray(), items));
    }

    public async Task<CustomResponse<RepositoryImportResponse>> AddAsync(CatalogAddRequest request, CancellationToken cancellationToken = default)
    {
        // Reuse path validation; the catalog does not create an implicit workspace.
        new CatalogAddValidator().ValidateAndThrowApp(request);
        LocalGitRepository? selected = null;
        try
        {
            var root = await inspector.InspectAsync(request.LocalPath, cancellationToken);
            if (string.Equals(root.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.LocalPath.Trim())), StringComparison.OrdinalIgnoreCase)) selected = root;
        }
        catch (ValidationAppException) { }
        IReadOnlyList<string> paths = selected is null ? folders.GetImmediateFolders(request.LocalPath, cancellationToken) : [selected.LocalPath];
        var results = new List<RepositoryImportItem>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var root = selected ?? await inspector.InspectAsync(path, cancellationToken);
                if (!string.Equals(root.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase))
                { results.Add(new(path, "ignored", LocalizedText.Get("import.not_root"))); continue; }
                var saved = await RegisterAsync(root, cancellationToken);
                results.Add(new(path, saved.Created ? "added" : "duplicate", LocalizedText.Get(saved.Created ? "import.added" : "catalog.already_registered")));
            }
            catch (ValidationAppException exception) { results.Add(new(path, "ignored", exception.Message)); }
            catch (OperationCanceledException) { throw; }
            catch (AppException exception) { results.Add(new(path, "failed", exception.Message)); }
            catch (Exception) { results.Add(new(path, "failed", LocalizedText.Get("error.internal"))); }
        }
        return CustomResponse<RepositoryImportResponse>.Ok(new(results), LocalizedText.Get("catalog.import_completed",
            results.Count(x => x.Status == "added"), results.Count(x => x.Status == "duplicate"), results.Count(x => x.Status == "ignored"), results.Count(x => x.Status == "failed")));
    }

    private Task<RegisteredRepository> RegisterAsync(LocalGitRepository root, CancellationToken cancellationToken) =>
        catalog.RegisterAsync(new GitRepository { Name = root.Name, LocalPath = root.LocalPath, PathKey = root.PathKey }, cancellationToken);

    private async Task<GitRepository> FindAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ValidationAppException(LocalizedText.Get("repository.invalid_id"));
        return await catalog.FindAsync(id, cancellationToken) ?? throw new NotFoundAppException(LocalizedText.Get("repository.link_not_found"));
    }
    public async Task<CustomResponse<RepositoryChangesResponse>> GetChangesAsync(int id, CancellationToken cancellationToken = default)
    {
        var repository = await FindAsync(id, cancellationToken);
        var snapshot = await changes.ReadAsync(repository.LocalPath, cancellationToken);
        return CustomResponse<RepositoryChangesResponse>.Ok(new(repository.Id, repository.Name, repository.LocalPath, snapshot.Status, snapshot.Files));
    }
    public async Task<CustomResponse<GitFileDiff>> GetDiffAsync(CatalogDiffRequest request, CancellationToken cancellationToken = default)
    {
        var repository = await FindAsync(request.RepositoryId, cancellationToken);
        new RepositoryDiffRequestValidator().ValidateAndThrowApp(new RepositoryDiffRequest(0, request.RepositoryId, request.Path, request.Area));
        return CustomResponse<GitFileDiff>.Ok(await changes.ReadDiffAsync(repository.LocalPath, request.Path, request.Area, cancellationToken));
    }
    public Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        new CatalogCloneValidator().ValidateAndThrowApp(request);
        return Task.FromResult(CustomResponse<RepositoryClonePlan>.Ok(cloner.Plan(request.RemoteUrl, request.ParentPath)));
    }
    public async Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default)
    {
        var plan = (await PreviewAsync(request, cancellationToken)).Result!;
        if (!string.Equals(plan.DestinationPath, request.DestinationPath, StringComparison.OrdinalIgnoreCase)) throw new ValidationAppException(LocalizedText.Get("clone.destination_changed"));
        var root = await cloner.CloneAsync(plan, cancellationToken);
        try
        {
            var saved = await RegisterAsync(root, cancellationToken);
            return CustomResponse<WorkspaceRepositoryResponse>.Ok(WorkspaceRepositoryResponse.FromEntity(saved.Repository), LocalizedText.Get("catalog.clone_completed", root.LocalPath));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { throw new OperationFailedAppException(LocalizedText.Get("catalog.clone_failed", root.LocalPath)); }
    }
}

