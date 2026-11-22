namespace GitCosmos.Domain.Entities;

public class WorkspaceRepository
{
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public int GitRepositoryId { get; set; }
    public GitRepository GitRepository { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

