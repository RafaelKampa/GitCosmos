namespace GitCosmos.Application.DTOs.Responses.Workspace;

public record WorkspaceResponse(int Id, string Name, DateTime CreatedAt, int RepositoryCount)
{
    public static WorkspaceResponse FromEntity(Domain.Entities.Workspace workspace) =>
        new(workspace.Id, workspace.Name, DateTime.SpecifyKind(workspace.CreatedAt, DateTimeKind.Utc), workspace.Repositories.Count);
}

