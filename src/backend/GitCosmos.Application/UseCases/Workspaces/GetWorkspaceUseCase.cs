using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Validators;
using GitCosmos.Application.Validators.Workspace;
using GitCosmos.Domain.Common;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.UseCases.Workspaces;

public class GetWorkspaceUseCase(IWorkspaceRepository workspaceRepository)
{
    public async Task<CustomResponse<PagedResult<WorkspaceResponse>>> GetAllAsync(PaginationRequest request, CancellationToken cancellationToken = default)
    {
        new WorkspacePaginationValidator().ValidateAndThrowApp(request);
        var page = request.GetPage();
        var size = request.GetPageSize();
        var result = await workspaceRepository.GetPageAsync(page, size, cancellationToken);
        return CustomResponse<PagedResult<WorkspaceResponse>>.Ok(
            new(result.Items.Select(WorkspaceResponse.FromEntity).ToList(), result.TotalRows, page, size), result.TotalRows);
    }
}

