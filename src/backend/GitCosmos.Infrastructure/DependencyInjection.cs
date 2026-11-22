using GitCosmos.Infrastructure.Data;
using GitCosmos.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GitCosmos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<GitCosmosDbContext>(options => options.UseSqlite(connectionString))
            .AddScoped<IWorkspaceRepository, Repositories.WorkspaceRepository>();
}
