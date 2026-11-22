namespace GitCosmos.Application.DTOs.Requests.Repository;

public record RepositoryChangesRequest(int WorkspaceId, int RepositoryId);
public record RepositoryDiffRequest(int WorkspaceId, int RepositoryId, string Path, string Area);
