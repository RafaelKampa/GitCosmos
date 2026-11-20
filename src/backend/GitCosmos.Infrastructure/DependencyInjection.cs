using GitCosmos.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GitCosmos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<GitCosmosDbContext>(options => options.UseSqlite(connectionString));
}
