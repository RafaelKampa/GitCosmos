using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.Services;
public class RepositoryConflictService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryConflicts conflicts) : IRepositoryConflictService
{
    private readonly RepositoryConflictUseCase _useCase = new(catalog, workspaces, links, conflicts);
    public Task<CustomResponse<RepositoryConflict>> ReadAsync(RepositoryConflictRequest r, CancellationToken ct) => _useCase.ReadAsync(r, ct);
    public Task<CustomResponse<ConflictResolutionPlan>> PrepareAsync(ResolveConflictRequest r, CancellationToken ct) => _useCase.PrepareAsync(r, ct);
    public Task<CustomResponse<bool>> ResolveAsync(ResolveConflictRequest r, CancellationToken ct) => _useCase.ResolveAsync(r, ct);
    public Task<CustomResponse<bool>> ExternalAsync(RepositoryConflictRequest r, CancellationToken ct) => _useCase.ExternalAsync(r, ct);
}
