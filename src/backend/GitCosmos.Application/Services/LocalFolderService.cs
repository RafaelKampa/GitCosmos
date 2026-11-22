using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.FileSystem;
using GitCosmos.Application.DTOs.Responses.FileSystem;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.FileSystem;

namespace GitCosmos.Application.Services;

public class LocalFolderService(ILocalFolderPicker picker) : ILocalFolderService
{
    private readonly SelectFolderUseCase _selectUseCase = new(picker);
    public Task<CustomResponse<FolderSelectionResponse>> SelectAsync(SelectFolderRequest request, CancellationToken cancellationToken = default) =>
        _selectUseCase.SelectAsync(request, cancellationToken);
}
