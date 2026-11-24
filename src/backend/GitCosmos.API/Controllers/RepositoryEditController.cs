using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryEditController(IRepositoryEditService service) : ControllerBase
{
    [HttpPost(Name = "RepositoryEdit_Prepare")]
    [Route("Prepare")]
    [EndpointSummary("Preflight stage, unstage or commit without modifying the repository")]
    [ProducesResponseType<CustomResponse<RepositoryEditPlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Prepare([FromBody] RepositoryEditRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost(Name = "RepositoryEdit_Execute")]
    [Route("Execute")]
    [EndpointSummary("Execute a reviewed operation only if the repository still matches its preflight token")]
    [ProducesResponseType<CustomResponse<RepositoryEditResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Execute([FromBody] ExecuteRepositoryEditRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ExecuteAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
