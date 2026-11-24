using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryHistoryController(IRepositoryHistoryService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryHistory_GetHistory")]
    [Route("GetHistory")]
    [EndpointSummary("Read a bounded page of local commit history with a reference snapshot")]
    [ProducesResponseType<CustomResponse<GitHistoryPage>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetHistory([FromQuery] RepositoryHistoryRequest request, CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryHistory_GetCommit")]
    [Route("GetCommit")]
    [EndpointSummary("Read commit metadata and changed files against its first parent, or the empty tree for a root commit")]
    [ProducesResponseType<CustomResponse<GitCommitDetails>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCommit([FromQuery] RepositoryCommitRequest request, CancellationToken cancellationToken)
    {
        var response = await service.GetCommitAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryHistory_GetDiff")]
    [Route("GetDiff")]
    [EndpointSummary("Read a bounded committed file diff without accessing the working tree")]
    [ProducesResponseType<CustomResponse<GitFileDiff>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiff([FromQuery] RepositoryCommitDiffRequest request, CancellationToken cancellationToken)
    {
        var response = await service.GetDiffAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
