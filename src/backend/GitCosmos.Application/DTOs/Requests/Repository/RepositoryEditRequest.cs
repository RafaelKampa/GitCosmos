namespace GitCosmos.Application.DTOs.Requests.Repository;

public record RepositoryEditRequest(int RepositoryId, int WorkspaceId, string Operation, string[] Paths,
    string Subject = "", string Description = "", bool Amend = false, bool Push = false);
public record ExecuteRepositoryEditRequest(RepositoryEditRequest Request, string Token);
