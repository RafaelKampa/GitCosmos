namespace GitCosmos.Application.DTOs.Requests.Repository;

public record RepositoryBranchQuery(int RepositoryId, int WorkspaceId = 0);
public record RebaseStep(string Sha, string Action);
public record RepositoryBranchRequest(int RepositoryId, int WorkspaceId, string Operation, string Name, string NewName = "",
    bool IsRemote = false, string Remote = "", string RemoteBranch = "", string Destination = "", string Message = "", string Upstream = "", string BaseBranch = "", RebaseStep[]? Steps = null, string Confirmation = "");
public record ExecuteRepositoryBranchRequest(RepositoryBranchRequest Request, string Token);
