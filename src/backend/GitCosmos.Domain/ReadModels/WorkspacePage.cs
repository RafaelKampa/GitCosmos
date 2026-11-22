using GitCosmos.Domain.Entities;

namespace GitCosmos.Domain.ReadModels;

public record WorkspacePage(List<Workspace> Items, int TotalRows);

