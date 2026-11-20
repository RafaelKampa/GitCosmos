using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.System;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.System;

namespace GitCosmos.Application.Services;

public class SystemService : ISystemService
{
    private readonly GetSystemStatusUseCase _getUseCase = new();

    public Task<CustomResponse<SystemStatusResponse>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        _getUseCase.GetStatusAsync(cancellationToken);
}
