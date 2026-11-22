using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.DTOs.Responses.Workspace;

public record RepositoryDashboardResponse(int Id, string Name, string LocalPath, string State, GitWorkingTreeStatus? Status, string? Error);
public record WorkspaceDashboardResponse(int WorkspaceId, string Name, DateTimeOffset CheckedAt, IReadOnlyList<RepositoryDashboardResponse> Repositories);
