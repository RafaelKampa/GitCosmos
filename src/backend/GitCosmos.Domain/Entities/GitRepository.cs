namespace GitCosmos.Domain.Entities;

public class GitRepository : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string PathKey { get; set; } = string.Empty;
    public ICollection<WorkspaceRepository> Workspaces { get; set; } = new List<WorkspaceRepository>();
}

