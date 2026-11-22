using GitCosmos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.Infrastructure.Data;

public class GitCosmosDbContext(DbContextOptions<GitCosmosDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<GitRepository> GitRepositories => Set<GitRepository>();
    public DbSet<Domain.Entities.WorkspaceRepository> WorkspaceRepositories => Set<Domain.Entities.WorkspaceRepository>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("workspaces");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            entity.Property(x => x.NameKey).HasColumnName("name_key").HasMaxLength(100);
            entity.HasIndex(x => x.NameKey).IsUnique().HasFilter("active = 1");
        });
        modelBuilder.Entity<GitRepository>(entity =>
        {
            entity.ToTable("git_repositories");
            entity.Property(x => x.Name).HasColumnName("name");
            entity.Property(x => x.LocalPath).HasColumnName("local_path");
            entity.Property(x => x.PathKey).HasColumnName("path_key");
            entity.HasIndex(x => x.PathKey).IsUnique();
        });
        modelBuilder.Entity<Domain.Entities.WorkspaceRepository>(entity =>
        {
            entity.ToTable("workspace_repositories");
            entity.HasKey(x => new { x.WorkspaceId, x.GitRepositoryId });
            entity.Property(x => x.WorkspaceId).HasColumnName("workspace_id");
            entity.Property(x => x.GitRepositoryId).HasColumnName("git_repository_id");
            entity.Property(x => x.AddedAt).HasColumnName("added_at");
            entity.HasOne(x => x.Workspace).WithMany(x => x.Repositories).HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.GitRepository).WithMany(x => x.Workspaces).HasForeignKey(x => x.GitRepositoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
