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
            .AddScoped<IRepositoryCatalogRepository, Repositories.RepositoryCatalogRepository>()
            .AddSingleton<IGitProcessRunner>(_ => new GitProcessRunner("git", TimeSpan.FromSeconds(10)))
            .AddSingleton<IBoundedGitProcessRunner>(_ => new GitProcessRunner("git", TimeSpan.FromSeconds(10)))
            .AddScoped<IRepositoryChangesReader, RepositoryChangesReader>()
            .AddScoped<IRepositoryHistoryReader, RepositoryHistoryReader>()
            .AddScoped<ICommitExplorer, CommitExplorer>()
            .AddScoped<IRepositoryConflicts, RepositoryConflicts>()
            .AddScoped<IRepositoryLineEditor, RepositoryLineEditor>()
            .AddScoped<ILocalFiles, LocalFiles>()
            .AddSingleton<IWorkingFileEditors, WorkingFileEditors>()
            .AddSingleton<IGitBlobReader>(_ => new GitBlobReader("git", TimeSpan.FromSeconds(10)))
            .AddSingleton<ICommitFileDesktop, CommitFileDesktop>()
            .AddScoped<IRepositoryEditor, RepositoryEditor>()
            .AddScoped<IRepositoryBranches, RepositoryBranches>()
            .AddSingleton<IGitMutationRunner, GitMutationRunner>()
            .AddScoped<IGitRepositoryInspector, GitRepositoryInspector>()
            .AddScoped<IRepositoryStatusReader, RepositoryStatusReader>()
            .AddSingleton<IWindowsFolderDialogFactory, WindowsFolderDialogFactory>()
            .AddSingleton<ILocalFolderPicker, WindowsFolderPicker>()
            .AddSingleton<IChildFolderReader, ChildFolderReader>()
            .AddSingleton<IGitCloneRunner, GitCloneRunner>()
            .AddScoped<IRepositoryCloner, RepositoryCloner>();
}
