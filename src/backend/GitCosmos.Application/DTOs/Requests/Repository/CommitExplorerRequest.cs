namespace GitCosmos.Application.DTOs.Requests.Repository;
public record CommitFileSelection(int RepositoryId, int WorkspaceId, string Sha, string Path = "");
public record CommitRestoreRequest(CommitFileSelection File, bool Before = false, string Token = "");
public record CommitDesktopRequest(CommitFileSelection File, string Action, string Tool = "");
