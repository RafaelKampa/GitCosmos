using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
namespace GitCosmos.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class LocalFilesController(ILocalFilesService service) : ControllerBase
{
    [HttpPost(Name = "LocalFiles_Read")]
    [Route("Read")]
    [EndpointSummary("Read local file metadata, patch, blame or line history")]
    [ProducesResponseType<CustomResponse<LocalFilesView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Read([FromBody] LocalFilesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ReadAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "LocalFiles_Prepare")]
    [Route("Prepare")]
    [EndpointSummary("Review discarding, stashing or ignoring explicitly selected paths")]
    [ProducesResponseType<CustomResponse<LocalFilesPlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Prepare([FromBody] LocalFilesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "LocalFiles_Execute")]
    [Route("Execute")]
    [EndpointSummary("Apply the reviewed local file operation after revalidation")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Execute([FromBody] LocalFilesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ExecuteAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "LocalFiles_Desktop")]
    [Route("Desktop")]
    [EndpointSummary("Open or compare a validated local file in an installed editor")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desktop([FromBody] LocalFilesRequest request, CancellationToken cancellationToken)
    {
        var response = await service.DesktopAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
