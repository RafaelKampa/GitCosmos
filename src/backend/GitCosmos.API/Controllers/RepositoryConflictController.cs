using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
namespace GitCosmos.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class RepositoryConflictController(IRepositoryConflictService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryConflict_GetConflict")]
    [Route("GetConflict")]
    [EndpointSummary("Read index stages 1, 2 and 3 and the working resolution of a conflicted file")]
    [ProducesResponseType<CustomResponse<RepositoryConflict>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetConflict([FromQuery] RepositoryConflictRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ReadAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryConflict_Prepare")]
    [Route("Prepare")]
    [EndpointSummary("Review saving and staging one conflicted file with a state fingerprint")]
    [ProducesResponseType<CustomResponse<ConflictResolutionPlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Prepare([FromBody] ResolveConflictRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryConflict_Resolve")]
    [Route("Resolve")]
    [EndpointSummary("Save the reviewed resolution and stage only its literal path")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve([FromBody] ResolveConflictRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ResolveAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryConflict_External")]
    [Route("External")]
    [EndpointSummary("Explicitly open a validated working file in the local text editor")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> External([FromBody] RepositoryConflictRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ExternalAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
