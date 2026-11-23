using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryCatalogService(IRepositoryCatalogRepository catalog, IRepositoryStatusReader status,
    IRepositoryChangesReader changes, IGitRepositoryInspector inspector, IChildFolderReader folders, IRepositoryCloner cloner) : IRepositoryCatalogService
{
    private readonly RepositoryCatalogUseCase _useCase = new(catalog, status, changes, inspector, folders, cloner);
    public Task<CustomResponse<RepositoryCatalogResponse>> GetAsync(bool includeStatus, CancellationToken cancellationToken = default) => _useCase.GetAsync(includeStatus, cancellationToken);
    public Task<CustomResponse<RepositoryImportResponse>> AddAsync(CatalogAddRequest request, CancellationToken cancellationToken = default) => _useCase.AddAsync(request, cancellationToken);
    public Task<CustomResponse<RepositoryChangesResponse>> GetChangesAsync(int repositoryId, CancellationToken cancellationToken = default) => _useCase.GetChangesAsync(repositoryId, cancellationToken);
    public Task<CustomResponse<GitFileDiff>> GetDiffAsync(CatalogDiffRequest request, CancellationToken cancellationToken = default) => _useCase.GetDiffAsync(request, cancellationToken);
    public Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default) => _useCase.PreviewAsync(request, cancellationToken);
    public Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default) => _useCase.CloneAsync(request, cancellationToken);
}
