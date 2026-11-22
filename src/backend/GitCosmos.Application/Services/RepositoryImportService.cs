using GitCosmos.Application.Common;
using GitCosmos.Application.DTOs.Requests.Workspace;
using GitCosmos.Application.DTOs.Responses.Workspace;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.UseCases.Workspaces;
using GitCosmos.Domain.Interfaces.Repositories;

namespace GitCosmos.Application.Services;

public class RepositoryImportService(IWorkspaceRepository workspaces, IRepositoryLinkRepository links,
    IGitRepositoryInspector inspector, IChildFolderReader folders) : IRepositoryImportService
{
    private readonly ImportWorkspaceRepositoriesUseCase _useCase = new(workspaces, links, inspector, folders);
    public Task<CustomResponse<RepositoryImportResponse>> ImportAsync(AddWorkspaceRepositoryRequest request, CancellationToken cancellationToken = default) =>
        _useCase.ImportAsync(request, cancellationToken);
}
