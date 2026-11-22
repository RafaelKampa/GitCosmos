namespace GitCosmos.Application.DTOs.Responses.Workspace;

public record RepositoryImportItem(string LocalPath, string Status, string Message);
public record RepositoryImportResponse(IReadOnlyList<RepositoryImportItem> Items);
