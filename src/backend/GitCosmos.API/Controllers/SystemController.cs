using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.System;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemController(ISystemService systemService) : ControllerBase
{
    [HttpGet(Name = "System_GetStatus")]
    [Route("GetStatus")]
    [EndpointSummary("Get local application status")]
    [ProducesResponseType<CustomResponse<SystemStatusResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var response = await systemService.GetStatusAsync(cancellationToken);
        return Ok(response);
    }
}
