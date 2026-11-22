using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Entities;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Domain.ReadModels;

namespace GitCosmos.Application.UseCases.Workspaces;

public class ImportWorkspaceRepositoriesUseCase(IWorkspaceRepository workspaces, IRepositoryLinkRepository links,
    IGitRepositoryInspector inspector, IChildFolderReader folders)
{
    public async Task<CustomResponse<RepositoryImportResponse>> ImportAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default)
    {
        new AddWorkspaceRepositoryRequestValidator().ValidateAndThrowApp(request);
        if (!await workspaces.ExistsAsync(request.WorkspaceId, cancellationToken))
            throw new NotFoundAppException(LocalizedText.Get("workspace.not_found"));
        LocalGitRepository? selected = null;
        try
        {
            var inspected = await inspector.InspectAsync(request.LocalPath, cancellationToken);
            if (string.Equals(inspected.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.LocalPath.Trim())), StringComparison.OrdinalIgnoreCase))
                selected = inspected;
        }
        catch (ValidationAppException) { /* A non-root folder is inspected as a container below. */ }
        // A repository root takes precedence; its child repositories must not be imported.
        IReadOnlyList<string> children = selected is null
            ? folders.GetImmediateFolders(request.LocalPath, cancellationToken)
            : [selected.LocalPath];
        var items = new List<RepositoryImportItem>();
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LocalGitRepository repository;
            try
            {
                repository = selected ?? await inspector.InspectAsync(child, cancellationToken);
                // rev-parse may find an ancestor repository: only a root at this exact child qualifies.
                if (!string.Equals(repository.LocalPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(child)), StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new(child, "ignored", LocalizedText.Get("import.not_root")));
                    continue;
                }
            }
            catch (ValidationAppException exception)
            {
                items.Add(new(child, "ignored", exception.Message));
                continue;
            }
            catch (OperationFailedAppException exception)
            {
                items.Add(new(child, "failed", exception.Message));
                continue;
            }
            try
            {
                var result = await links.AddAsync(request.WorkspaceId, new GitRepository
                    { Name = repository.Name, LocalPath = repository.LocalPath, PathKey = repository.PathKey }, cancellationToken);
                var status = result.Status switch
                {
                    RepositoryLinkStatus.Added => "added",
                    RepositoryLinkStatus.Duplicate => "duplicate",
                    _ => "failed"
                };
                items.Add(new(child, status, LocalizedText.Get(result.Status == RepositoryLinkStatus.WorkspaceNotFound
                    ? "workspace.not_found" : "import." + status)));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // Earlier links remain committed; never expose persistence diagnostics to the client.
                items.Add(new(child, "failed", LocalizedText.Get("error.internal")));
            }
        }
        return CustomResponse<RepositoryImportResponse>.Ok(new(items), LocalizedText.Get("import.completed",
            items.Count(x => x.Status == "added"), items.Count(x => x.Status == "duplicate"),
            items.Count(x => x.Status == "ignored"), items.Count(x => x.Status == "failed")));
    }
}
