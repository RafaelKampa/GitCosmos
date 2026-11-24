using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryEditService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryEditor editor) : IRepositoryEditService
{
    private readonly RepositoryEditUseCase _useCase = new(catalog, workspaces, links, editor);
    public Task<CustomResponse<RepositoryEditPlan>> PrepareAsync(RepositoryEditRequest request, CancellationToken cancellationToken = default) => _useCase.PrepareAsync(request, cancellationToken);
    public Task<CustomResponse<RepositoryEditResult>> ExecuteAsync(ExecuteRepositoryEditRequest request, CancellationToken cancellationToken = default) => _useCase.ExecuteAsync(request, cancellationToken);
}
