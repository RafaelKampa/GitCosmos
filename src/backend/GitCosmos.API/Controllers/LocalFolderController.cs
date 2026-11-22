using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.FileSystem;
using GitCosmos.Application.DTOs.Responses.FileSystem;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocalFolderController(ILocalFolderService folderService) : ControllerBase
{
    [HttpPost(Name = "LocalFolder_SelectFolder")]
    [Route("SelectFolder")]
    [Consumes("application/json")]
    [EndpointSummary("Open the Windows folder picker on the computer running the local API")]
    [ProducesResponseType<CustomResponse<FolderSelectionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SelectFolder([FromBody] SelectFolderRequest request, CancellationToken cancellationToken)
    {
        var response = await folderService.SelectAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
