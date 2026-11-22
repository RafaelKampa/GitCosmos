using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Consumes("application/json")]
public class RepositoryCloneController(IRepositoryCloneService cloneService) : ControllerBase
{
    [HttpPost(Name = "RepositoryClone_Preview")]
    [Route("Preview")]
    [EndpointSummary("Preview the new repository folder before confirming a clone")]
    [ProducesResponseType<CustomResponse<RepositoryClonePlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Preview([FromBody] PreviewRepositoryCloneRequest request, CancellationToken cancellationToken)
    {
        var response = await cloneService.PreviewAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost(Name = "RepositoryClone_CloneRepository")]
    [Route("CloneRepository")]
    [EndpointSummary("Clone to the confirmed new folder and link it to the workspace")]
    [ProducesResponseType<CustomResponse<WorkspaceRepositoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CloneRepository([FromBody] CloneWorkspaceRepositoryRequest request, CancellationToken cancellationToken)
    {
        var response = await cloneService.CloneAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
