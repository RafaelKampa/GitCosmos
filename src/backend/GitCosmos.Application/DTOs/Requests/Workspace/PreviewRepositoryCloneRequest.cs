namespace GitCosmos.Application.DTOs.Requests.Workspace;

public record PreviewRepositoryCloneRequest(int WorkspaceId, string RemoteUrl, string ParentPath);
public record CloneWorkspaceRepositoryRequest(int WorkspaceId, string RemoteUrl, string ParentPath, string DestinationPath);
