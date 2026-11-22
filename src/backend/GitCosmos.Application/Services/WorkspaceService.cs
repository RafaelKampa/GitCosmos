using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Workspaces;
using GitCosmos.Domain.Common;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class WorkspaceService(IWorkspaceRepository workspaceRepository) : IWorkspaceService
{
    private readonly CreateWorkspaceUseCase _createUseCase = new(workspaceRepository);
    private readonly GetWorkspaceUseCase _getUseCase = new(workspaceRepository);

    public Task<CustomResponse<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default) =>
        _createUseCase.CreateAsync(request, cancellationToken);

    public Task<CustomResponse<PagedResult<WorkspaceResponse>>> GetAllAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        _getUseCase.GetAllAsync(request, cancellationToken);
}

