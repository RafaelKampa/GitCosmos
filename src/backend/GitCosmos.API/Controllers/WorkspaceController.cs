using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkspaceController(IWorkspaceService workspaceService) : ControllerBase
{
    [HttpPost(Name = "Workspace_CreateWorkspace")]
    [Route("CreateWorkspace")]
    [EndpointSummary("Create a local workspace")]
    [ProducesResponseType<CustomResponse<WorkspaceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var response = await workspaceService.CreateAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "Workspace_GetAllWorkspaces")]
    [Route("GetAllWorkspaces")]
    [EndpointSummary("List local workspaces with zero-based pagination")]
    [ProducesResponseType<CustomResponse<PagedResult<WorkspaceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAllWorkspaces([FromQuery] PaginationRequest paginationRequest, CancellationToken cancellationToken)
    {
        var response = await workspaceService.GetAllAsync(paginationRequest, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}

