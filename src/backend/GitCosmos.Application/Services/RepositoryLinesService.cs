using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.Services;
public class RepositoryLinesService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryLineEditor editor) : IRepositoryLinesService
{
    private readonly RepositoryLinesUseCase _useCase = new(catalog, workspaces, links, editor);
    public Task<CustomResponse<RepositoryLinesSnapshot>> ReadAsync(RepositoryLinesRequest r, CancellationToken ct) => _useCase.ReadAsync(r, ct);
    public Task<CustomResponse<RepositoryLinesPlan>> PrepareAsync(PrepareLinesRequest r, CancellationToken ct) => _useCase.PrepareAsync(r, ct);
    public Task<CustomResponse<bool>> ExecuteAsync(ExecuteLinesRequest r, CancellationToken ct) => _useCase.ExecuteAsync(r, ct);
}
