using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryChangesController(IRepositoryChangesService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryChanges_GetChanges")]
    [Route("GetChanges")]
    [EndpointSummary("List local changes of a repository linked to an active workspace")]
    [ProducesResponseType<CustomResponse<RepositoryChangesResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChanges([FromQuery] int workspaceId, [FromQuery] int repositoryId, CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(new(workspaceId, repositoryId), cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryChanges_GetDiff")]
    [Route("GetDiff")]
    [EndpointSummary("Read a bounded diff of a currently changed path in the linked repository")]
    [ProducesResponseType<CustomResponse<GitFileDiff>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetDiff([FromQuery] RepositoryDiffRequest request, CancellationToken cancellationToken)
    {
        var response = await service.GetDiffAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
