using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RepositoryCatalogController(IRepositoryCatalogService service) : ControllerBase
{
    [HttpGet(Name = "RepositoryCatalog_GetExplorer")]
    [Route("GetExplorer")]
    [ProducesResponseType<CustomResponse<RepositoryCatalogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExplorer(CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(false, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryCatalog_GetOverview")]
    [Route("GetOverview")]
    [ProducesResponseType<CustomResponse<RepositoryCatalogResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var response = await service.GetAsync(true, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost(Name = "RepositoryCatalog_AddRepository")]
    [Route("AddRepository")]
    [ProducesResponseType<CustomResponse<RepositoryImportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRepository([FromBody] CatalogAddRequest request, CancellationToken cancellationToken)
    {
        var response = await service.AddAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryCatalog_GetChanges")]
    [Route("GetChanges")]
    [ProducesResponseType<CustomResponse<RepositoryChangesResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChanges([FromQuery] int repositoryId, CancellationToken cancellationToken)
    {
        var response = await service.GetChangesAsync(repositoryId, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpGet(Name = "RepositoryCatalog_GetDiff")]
    [Route("GetDiff")]
    [ProducesResponseType<CustomResponse<GitFileDiff>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDiff([FromQuery] CatalogDiffRequest request, CancellationToken cancellationToken)
    {
        var response = await service.GetDiffAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost(Name = "RepositoryCatalog_Preview")]
    [Route("Preview")]
    [ProducesResponseType<CustomResponse<RepositoryClonePlan>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview([FromBody] CatalogCloneRequest request, CancellationToken cancellationToken)
    {
        var response = await service.PreviewAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

    [HttpPost(Name = "RepositoryCatalog_CloneRepository")]
    [Route("CloneRepository")]
    [ProducesResponseType<CustomResponse<WorkspaceRepositoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloneRepository([FromBody] CatalogCloneRequest request, CancellationToken cancellationToken)
    {
        var response = await service.CloneAsync(request, cancellationToken);
        if (!response.Success) return BadRequest(response);
        return Ok(response);
    }

}
