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
    private readonly UpdateWorkspaceUseCase _updateUseCase = new(workspaceRepository);
    private readonly DeleteWorkspaceUseCase _deleteUseCase = new(workspaceRepository);

    public Task<CustomResponse<WorkspaceResponse>> UpdateAsync(UpdateWorkspaceRequest request, CancellationToken cancellationToken = default) =>
        _updateUseCase.UpdateAsync(request, cancellationToken);

    public Task<CustomResponse<int?>> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        _deleteUseCase.DeleteAsync(id, cancellationToken);

    public Task<CustomResponse<WorkspaceResponse>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken = default) =>
        _createUseCase.CreateAsync(request, cancellationToken);

    public Task<CustomResponse<PagedResult<WorkspaceResponse>>> GetAllAsync(PaginationRequest request, CancellationToken cancellationToken = default) =>
        _getUseCase.GetAllAsync(request, cancellationToken);
}

