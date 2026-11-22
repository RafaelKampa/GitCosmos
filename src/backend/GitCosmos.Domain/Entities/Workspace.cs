namespace GitCosmos.Domain.Entities;

public class Workspace : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public ICollection<WorkspaceRepository> Repositories { get; set; } = new List<WorkspaceRepository>();
}

