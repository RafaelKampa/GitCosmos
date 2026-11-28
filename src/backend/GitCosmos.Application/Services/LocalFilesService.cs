using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;
namespace GitCosmos.Application.Services;
public class LocalFilesService(IRepositoryCatalogRepository catalog, IWorkspaceRepository workspaces, IRepositoryLinkRepository links, ILocalFiles files) : ILocalFilesService
{
    private readonly LocalFilesUseCase _useCase = new(catalog, workspaces, links, files);
    public Task<CustomResponse<LocalFilesView>> ReadAsync(LocalFilesRequest r, CancellationToken ct) => _useCase.ReadAsync(r, ct);
    public Task<CustomResponse<LocalFilesPlan>> PrepareAsync(LocalFilesRequest r, CancellationToken ct) => _useCase.PrepareAsync(r, ct);
    public Task<CustomResponse<bool>> ExecuteAsync(LocalFilesRequest r, CancellationToken ct) => _useCase.ExecuteAsync(r, ct);
    public Task<CustomResponse<bool>> DesktopAsync(LocalFilesRequest r, CancellationToken ct) => _useCase.DesktopAsync(r, ct);
}
