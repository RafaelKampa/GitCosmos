namespace GitCosmos.Application.DTOs.Requests.Repository;
public record RepositoryConflictRequest(int RepositoryId, string Path, int WorkspaceId = 0);
public record ResolveConflictRequest(RepositoryConflictRequest File, string Action, string Content = "", string Token = "");
