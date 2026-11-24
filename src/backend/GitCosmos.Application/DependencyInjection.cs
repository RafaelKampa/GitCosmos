using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitCosmos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddScoped<ISystemService, SystemService>()
            .AddScoped<IWorkspaceService, WorkspaceService>()
            .AddScoped<IWorkspaceRepositoryService, WorkspaceRepositoryService>()
            .AddScoped<ILocalFolderService, LocalFolderService>()
            .AddScoped<IRepositoryCloneService, RepositoryCloneService>()
            .AddScoped<IRepositoryImportService, RepositoryImportService>()
            .AddScoped<IWorkspaceDashboardService, WorkspaceDashboardService>()
            .AddScoped<IRepositoryChangesService, RepositoryChangesService>()
            .AddScoped<IRepositoryHistoryService, RepositoryHistoryService>()
            .AddScoped<IRepositoryEditService, RepositoryEditService>()
            .AddScoped<IRepositoryCatalogService, RepositoryCatalogService>();
}
