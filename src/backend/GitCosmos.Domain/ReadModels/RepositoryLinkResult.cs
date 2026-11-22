using GitCosmos.Domain.Entities;

namespace GitCosmos.Domain.ReadModels;

public enum RepositoryLinkStatus { Added, Duplicate, WorkspaceNotFound }
public record RepositoryLinkResult(RepositoryLinkStatus Status, GitRepository? Repository = null);
