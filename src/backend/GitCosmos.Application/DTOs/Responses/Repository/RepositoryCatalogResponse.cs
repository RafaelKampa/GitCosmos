using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.DTOs.Responses.Repository;

public record CatalogWorkspace(int Id, string Name);
public record CatalogRepository(int Id, string Name, string LocalPath, IReadOnlyList<int> WorkspaceIds,
    string State = "unknown", GitWorkingTreeStatus? Status = null, string? Error = null);
public record RepositoryCatalogResponse(IReadOnlyList<CatalogWorkspace> Workspaces, IReadOnlyList<CatalogRepository> Repositories);
