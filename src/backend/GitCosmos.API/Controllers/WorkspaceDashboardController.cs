using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkspaceDashboardController(IWorkspaceDashboardService service) : ControllerBase
{
    [HttpGet(Name = "WorkspaceDashboard_GetDashboard")]
    [Route("GetDashboard")]
    [EndpointSummary("Read the local branch and working tree status of each linked repository")]
    [ProducesResponseType<CustomResponse<WorkspaceDashboardResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard([FromQuery] int workspaceId, CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(workspaceId, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
