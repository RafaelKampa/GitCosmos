namespace GitCosmos.Application.DTOs.Requests.Repository;
public record LocalFilesRequest(int RepositoryId, string[] Paths, string Area, string Action, int WorkspaceId = 0, string Token = "", string Tool = "", string Pattern = "file", int Start = 1, int End = 1);
