using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.FileSystem;
using GitCosmos.Application.DTOs.Responses.FileSystem;

namespace GitCosmos.Application.Interfaces.Services;

public interface ILocalFolderService
{
    Task<CustomResponse<FolderSelectionResponse>> SelectAsync(SelectFolderRequest request, CancellationToken cancellationToken = default);
}
