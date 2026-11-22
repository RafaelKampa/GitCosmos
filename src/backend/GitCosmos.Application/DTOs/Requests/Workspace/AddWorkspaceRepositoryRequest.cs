namespace GitCosmos.Application.DTOs.Requests.Workspace;

public record AddWorkspaceRepositoryRequest(int WorkspaceId, string LocalPath);
