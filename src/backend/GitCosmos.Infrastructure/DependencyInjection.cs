using GitCosmos.Infrastructure.Data;
using GitCosmos.Domain.Interfaces.Repositories;
using GitCosmos.Application.Interfaces.Git;
using GitCosmos.Infrastructure.Git;
using GitCosmos.Application.Interfaces.FileSystem;
using GitCosmos.Infrastructure.FileSystem;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GitCosmos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<GitCosmosDbContext>(options => options.UseSqlite(connectionString))
            .AddScoped<IWorkspaceRepository, Repositories.WorkspaceRepository>()
            .AddScoped<IRepositoryLinkRepository, Repositories.RepositoryLinkRepository>()
            .AddSingleton<IGitProcessRunner>(_ => new GitProcessRunner("git", TimeSpan.FromSeconds(10)))
            .AddScoped<IGitRepositoryInspector, GitRepositoryInspector>()
            .AddSingleton<IWindowsFolderDialogFactory, WindowsFolderDialogFactory>()
            .AddSingleton<ILocalFolderPicker, WindowsFolderPicker>()
            .AddSingleton<IChildFolderReader, ChildFolderReader>()
            .AddSingleton<IGitCloneRunner, GitCloneRunner>()
            .AddScoped<IRepositoryCloner, RepositoryCloner>();
}
