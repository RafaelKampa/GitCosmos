using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.DTOs.Responses.Repository;

public record RepositoryChangesResponse(int Id, string Name, string LocalPath, GitWorkingTreeStatus Status, IReadOnlyList<GitFileChange> Files);
