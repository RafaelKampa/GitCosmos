namespace GitCosmos.Application.DTOs.Responses.Workspace;

public record WorkspaceRepositoryResponse(int Id, string Name, string LocalPath)
{
    public static WorkspaceRepositoryResponse FromEntity(Domain.Entities.GitRepository repository) =>
        new(repository.Id, repository.Name, repository.LocalPath);
}
