using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Workspaces;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class WorkspaceRepositoryService(IWorkspaceRepository workspaceRepository, IRepositoryLinkRepository linkRepository, IGitRepositoryInspector inspector) : IWorkspaceRepositoryService
{
    private readonly AddWorkspaceRepositoryUseCase _addUseCase = new(workspaceRepository, linkRepository, inspector);
    private readonly GetWorkspaceRepositoriesUseCase _getUseCase = new(workspaceRepository, linkRepository);
    private readonly RemoveWorkspaceRepositoryUseCase _removeUseCase = new(workspaceRepository, linkRepository);

    public Task<CustomResponse<WorkspaceRepositoryResponse>> AddAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default) =>
        _addUseCase.AddAsync(request, cancellationToken);
    public Task<CustomResponse<List<WorkspaceRepositoryResponse>>> GetAllAsync(int workspaceId, CancellationToken cancellationToken = default) =>
        _getUseCase.GetAllAsync(workspaceId, cancellationToken);
    public Task<CustomResponse<int?>> RemoveAsync(int workspaceId, int repositoryId, CancellationToken cancellationToken = default) =>
        _removeUseCase.RemoveAsync(workspaceId, repositoryId, cancellationToken);
}
