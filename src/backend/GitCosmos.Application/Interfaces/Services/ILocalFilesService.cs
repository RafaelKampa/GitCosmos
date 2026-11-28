using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
namespace GitCosmos.Application.Interfaces.Services;
public interface ILocalFilesService
{
    Task<CustomResponse<LocalFilesView>> ReadAsync(LocalFilesRequest request, CancellationToken ct);
    Task<CustomResponse<LocalFilesPlan>> PrepareAsync(LocalFilesRequest request, CancellationToken ct);
    Task<CustomResponse<bool>> ExecuteAsync(LocalFilesRequest request, CancellationToken ct);
    Task<CustomResponse<bool>> DesktopAsync(LocalFilesRequest request, CancellationToken ct);
}
