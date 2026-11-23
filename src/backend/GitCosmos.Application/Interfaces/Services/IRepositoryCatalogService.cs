using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Application.Interfaces.Services;

public interface IRepositoryCatalogService
{
    Task<CustomResponse<RepositoryCatalogResponse>> GetAsync(bool includeStatus, CancellationToken cancellationToken = default);
    Task<CustomResponse<RepositoryImportResponse>> AddAsync(CatalogAddRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<RepositoryChangesResponse>> GetChangesAsync(int repositoryId, CancellationToken cancellationToken = default);
    Task<CustomResponse<GitFileDiff>> GetDiffAsync(CatalogDiffRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default);
    Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CatalogCloneRequest request, CancellationToken cancellationToken = default);
}
