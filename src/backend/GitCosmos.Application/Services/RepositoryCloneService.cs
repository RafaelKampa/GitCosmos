using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Workspaces;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryCloneService(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryCloner cloner) : IRepositoryCloneService
{
    private readonly CloneWorkspaceRepositoryUseCase _useCase = new(workspaces, links, cloner);
    public Task<CustomResponse<RepositoryClonePlan>> PreviewAsync(PreviewRepositoryCloneRequest request, CancellationToken cancellationToken = default) =>
        _useCase.PreviewAsync(request, cancellationToken);
    public Task<CustomResponse<WorkspaceRepositoryResponse>> CloneAsync(CloneWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default) =>
        _useCase.CloneAsync(request, cancellationToken);
}
