using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public class RepositoryBranchController(IRepositoryBranchService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryBranch_GetResources")]
    [Route("GetResources")]
    [EndpointSummary("Read configured remotes, remote tracking branches and pending merge/rebase state without fetching")]
    [ProducesResponseType<CustomResponse<BranchResources>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResources([FromQuery] RepositoryBranchQuery request, CancellationToken cancellationToken)
    {
        var response = await service.GetResourcesAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpGet(Name = "RepositoryBranch_GetBranches")]
    [Route("GetBranches")]
    [EndpointSummary("List local branches and their locally known upstreams without fetching")]
    [ProducesResponseType<CustomResponse<LocalGitBranches>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranches([FromQuery] RepositoryBranchQuery request, CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryBranch_Prepare")]
    [Route("Prepare")]
    [EndpointSummary("Review a local/remote branch, worktree, tag, tracking, merge, rebase or recovery operation without modifying Git")]
    [ProducesResponseType<CustomResponse<RepositoryBranchPlan>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Prepare([FromBody] RepositoryBranchRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "RepositoryBranch_Execute")]
    [Route("Execute")]
    [EndpointSummary("Execute the reviewed local branch operation if its state token still matches")]
    [ProducesResponseType<CustomResponse<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Execute([FromBody] ExecuteRepositoryBranchRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ExecuteAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
