namespace GitCosmos.Application.DTOs.Requests.Repository;

public record RepositoryHistoryRequest(int RepositoryId, int WorkspaceId = 0, int Page = 0, int PageSize = 50, string? Snapshot = null);
public record RepositoryCommitRequest(int RepositoryId, string Sha, int WorkspaceId = 0);
public record RepositoryCommitDiffRequest(int RepositoryId, string Sha, string Path, int WorkspaceId = 0);
