using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkspaceRepositoryController(IWorkspaceRepositoryService repositoryService) : ControllerBase
{
    [HttpPost(Name = "WorkspaceRepository_AddRepository")]
    [Route("AddRepository")]
    [EndpointSummary("Validate and link an existing local Git working tree to a workspace")]
    [ProducesResponseType<CustomResponse<WorkspaceRepositoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddRepository([FromBody] AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken)
    {
        var response = await repositoryService.AddAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "WorkspaceRepository_GetRepositories")]
    [Route("GetRepositories")]
    [EndpointSummary("List repositories linked to an active workspace")]
    [ProducesResponseType<CustomResponse<List<WorkspaceRepositoryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRepositories([FromQuery] int workspaceId, CancellationToken cancellationToken)
    {
        var response = await repositoryService.GetAllAsync(workspaceId, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpDelete(Name = "WorkspaceRepository_RemoveRepository")]
    [Route("RemoveRepository")]
    [EndpointSummary("Remove only the workspace link, preserving the repository and its files")]
    [ProducesResponseType<CustomResponse<int?>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRepository([FromQuery] int workspaceId, [FromQuery] int repositoryId, CancellationToken cancellationToken)
    {
        var response = await repositoryService.RemoveAsync(workspaceId, repositoryId, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
