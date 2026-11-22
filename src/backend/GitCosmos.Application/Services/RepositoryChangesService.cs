using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.DTOs.Responses.Repository;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Repositories;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryChangesService(IWorkspaceRepository workspaces, IRepositoryLinkRepository links, IRepositoryChangesReader reader) : IRepositoryChangesService
{
    private readonly GetRepositoryChangesUseCase _useCase = new(workspaces, links, reader);
    public Task<CustomResponse<RepositoryChangesResponse>> GetAsync(RepositoryChangesRequest request, CancellationToken cancellationToken = default) => _useCase.GetAsync(request, cancellationToken);
    public Task<CustomResponse<GitFileDiff>> GetDiffAsync(RepositoryDiffRequest request, CancellationToken cancellationToken = default) => _useCase.GetDiffAsync(request, cancellationToken);
}
