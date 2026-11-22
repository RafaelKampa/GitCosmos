using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.FileSystem;
using GitCosmos.Application.DTOs.Responses.FileSystem;
using GitCosmos.Application.Interfaces.FileSystem;

namespace GitCosmos.Application.UseCases.FileSystem;

public class SelectFolderUseCase(ILocalFolderPicker picker)
{
    public async Task<CustomResponse<FolderSelectionResponse>> SelectAsync(SelectFolderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.InitialPath?.Length > 32767)
            throw new ValidationAppException(LocalizedText.Get("repository.invalid_path"));
        var path = await picker.PickAsync(LocalizedText.Get("folder.title"), LocalizedText.Get("folder.confirm"),
            request.InitialPath?.Trim(), cancellationToken);
        return CustomResponse<FolderSelectionResponse>.Ok(new(path, path is null));
    }
}
