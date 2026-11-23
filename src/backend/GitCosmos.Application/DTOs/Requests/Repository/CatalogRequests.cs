namespace GitCosmos.Application.DTOs.Requests.Repository;

public record CatalogAddRequest(string LocalPath);
public record CatalogCloneRequest(string RemoteUrl, string ParentPath, string? DestinationPath = null);
public record CatalogDiffRequest(int RepositoryId, string Path, string Area);
