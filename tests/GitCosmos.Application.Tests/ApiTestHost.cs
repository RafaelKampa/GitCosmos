using GitCosmos.API;
using Microsoft.AspNetCore.Builder;

namespace GitCosmos.Application.Tests;

internal static class ApiTestHost
{
    public static string WebRootPath => Path.Combine(AppContext.BaseDirectory, "wwwroot");

    // Keep the real static-file middleware while providing the test host's own assets.
    // An explicit absolute web root also works when a test changes the content root.
    public static WebApplication Build(params string[] args) =>
        ApiHost.Build([.. args, "--webroot=" + WebRootPath]);
}
