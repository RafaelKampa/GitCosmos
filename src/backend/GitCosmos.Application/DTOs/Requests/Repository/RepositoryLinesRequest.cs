namespace GitCosmos.Application.DTOs.Requests.Repository;
public record RepositoryLinesRequest(int RepositoryId, string Path, string Area, int WorkspaceId = 0);
public record PrepareLinesRequest(RepositoryLinesRequest File, int[] Lines, string Token, string Operation = "move", bool ExportPatch = false);
public record ExecuteLinesRequest(PrepareLinesRequest Request, string Token);
