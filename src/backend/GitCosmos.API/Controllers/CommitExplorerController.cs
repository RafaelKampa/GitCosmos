using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Common;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommitExplorerController(ICommitExplorerService service) : ControllerBase
{
    [HttpGet(Name = "CommitExplorer_Tree")]
    [Route("Tree")]
    [ProducesResponseType<CustomResponse<IReadOnlyList<CommitTreeEntry>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Read the bounded complete file tree at an immutable commit")]
    public async Task<IActionResult> Tree([FromQuery] RepositoryCommitRequest request, CancellationToken cancellationToken)
    {
        var response = await service.TreeAsync(new(request.RepositoryId, request.WorkspaceId, request.Sha), cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpGet(Name = "CommitExplorer_File")]
    [Route("File")]
    [ProducesResponseType<CustomResponse<CommitFileContent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Read a bounded blob at an immutable commit, including binary download bytes")]
    public async Task<IActionResult> FileContent([FromQuery] CommitFileSelection request, CancellationToken cancellationToken)
    {
        var response = await service.FileAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpGet(Name = "CommitExplorer_History")]
    [Route("History")]
    [ProducesResponseType<CustomResponse<IReadOnlyList<GitCommit>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Read up to 101 file commits following renames from the selected commit")]
    public async Task<IActionResult> History([FromQuery] CommitFileSelection request, CancellationToken cancellationToken)
    {
        var response = await service.HistoryAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpGet(Name = "CommitExplorer_Blame")]
    [Route("Blame")]
    [ProducesResponseType<CustomResponse<IReadOnlyList<CommitBlameLine>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Read bounded line attribution at the selected commit")]
    public async Task<IActionResult> Blame([FromQuery] CommitFileSelection request, CancellationToken cancellationToken)
    {
        var response = await service.BlameAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpGet(Name = "CommitExplorer_Tools")]
    [Route("Tools")]
    [ProducesResponseType<CustomResponse<IReadOnlyList<CommitExternalTool>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("List detected supported desktop diff applications")]
    public async Task<IActionResult> Tools([FromQuery] RepositoryCommitRequest request, CancellationToken cancellationToken)
    {
        var response = await service.ToolsAsync(new(request.RepositoryId, request.WorkspaceId, request.Sha), cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "CommitExplorer_PrepareRestore")]
    [Route("PrepareRestore")]
    [ProducesResponseType<CustomResponse<CommitRestorePlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Review restoring one working-tree file, leaving the Git index unchanged")]
    public async Task<IActionResult> PrepareRestore([FromBody] CommitRestoreRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PrepareAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "CommitExplorer_Restore")]
    [Route("Restore")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Restore a reviewed file only if its reviewed working state still matches")]
    public async Task<IActionResult> Restore([FromBody] CommitRestoreRequest request, CancellationToken cancellationToken)
    {
        var response = await service.RestoreAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
    [HttpPost(Name = "CommitExplorer_Desktop")]
    [Route("Desktop")]
    [ProducesResponseType<CustomResponse<bool>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [EndpointSummary("Explicitly open, reveal or externally compare a selected committed file")]
    public async Task<IActionResult> Desktop([FromBody] CommitDesktopRequest request, CancellationToken cancellationToken)
    {
        var response = await service.DesktopAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }
}
