using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitCosmos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddScoped<ISystemService, SystemService>();
}
