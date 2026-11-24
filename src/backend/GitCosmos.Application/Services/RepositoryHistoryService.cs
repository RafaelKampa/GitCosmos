using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryHistoryService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces,
    IRepositoryLinkRepository links, IRepositoryHistoryReader reader) : IRepositoryHistoryService
{
    private readonly RepositoryHistoryUseCase _useCase = new(catalog, workspaces, links, reader);
    public Task<CustomResponse<GitHistoryPage>> GetAsync(RepositoryHistoryRequest request, CancellationToken cancellationToken = default) => _useCase.GetAsync(request, cancellationToken);
    public Task<CustomResponse<GitCommitDetails>> GetCommitAsync(RepositoryCommitRequest request, CancellationToken cancellationToken = default) => _useCase.GetCommitAsync(request, cancellationToken);
    public Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryCommitDiffRequest request, CancellationToken cancellationToken = default) => _useCase.GetDiffAsync(request, cancellationToken);
}
