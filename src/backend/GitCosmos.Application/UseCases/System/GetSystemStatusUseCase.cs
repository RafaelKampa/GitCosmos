using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Responses.System;

namespace GitCosmos.Application.UseCases.System;

public class GetSystemStatusUseCase
{
    public Task<CustomResponse<SystemStatusResponse>> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CustomResponse<SystemStatusResponse>.Ok(
            new SystemStatusResponse("GitCosmos", "0.1.0-dev", RequestLanguage.Current),
            LocalizedText.Get("system.ready")));
    }
}
