using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
namespace GitCosmos.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class RepositoryLinesController(IRepositoryLinesService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryLines_GetSelection")]
    [Route("GetSelection")]
    [EndpointSummary("Read the canonical local diff and fingerprint for individual line selection")]
    [ProducesResponseType<CustomResponse<RepositoryLinesSnapshot>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetSelection([FromQuery] RepositoryLinesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ReadAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryLines_Prepare")]
    [Route("Prepare")]
    [EndpointSummary("Prepare selected diff lines for staging, unstaging, discard review or patch export")]
    [ProducesResponseType<CustomResponse<RepositoryLinesPlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Prepare([FromBody] PrepareLinesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryLines_Execute")]
    [Route("Execute")]
    [EndpointSummary("Revalidate and stage, unstage or explicitly discard the reviewed changed lines")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Execute([FromBody] ExecuteLinesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ExecuteAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
