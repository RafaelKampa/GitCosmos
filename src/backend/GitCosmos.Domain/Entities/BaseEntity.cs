using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GitCosmos.Domain.Entities;

public abstract class BaseEntity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity), Column("id")]
    public int Id { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
    [Column("active")]
    public bool Active { get; set; } = true;
    [NotMapped]
    public int TotalRows { get; set; }

    public void Activate(DateTime? changedAt = null)
    {
        Active = true;
        DeletedAt = null;
        UpdatedAt = changedAt ?? DateTime.UtcNow;
    }

    public void Deactivate(DateTime? changedAt = null)
    {
        Active = false;
        DeletedAt = changedAt ?? DateTime.UtcNow;
        UpdatedAt = DeletedAt;
    }

    public void RefreshUpdatedAt() => UpdatedAt = DateTime.UtcNow;
}

