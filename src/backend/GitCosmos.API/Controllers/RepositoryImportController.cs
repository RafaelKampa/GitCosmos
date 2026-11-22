using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryImportController(IRepositoryImportService service) : ControllerBase
{
    [HttpPost(Name = "RepositoryImport_ImportRepositories")]
    [Route("ImportRepositories")]
    [Consumes("application/json")]
    [EndpointSummary("Link the selected Git root, or discover roots in its immediate child folders")]
    [ProducesResponseType<CustomResponse<RepositoryImportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ImportRepositories([FromBody] AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ImportAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
