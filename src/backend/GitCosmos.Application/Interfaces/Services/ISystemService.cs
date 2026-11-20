using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.System;

namespace GitCosmos.Application.Interfaces.Services;

public interface ISystemService
{
    Task<CustomResponse<SystemStatusResponse>> GetStatusAsync(CancellationToken cancellationToken = default);
}
