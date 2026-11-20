using GitCosmos.API.Extensions;
using GitCosmos.API.Middlewares;
using GitCosmos.Application;
using GitCosmos.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;

namespace GitCosmos.API;

public static class ApiHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls("http://127.0.0.1:5210");
        builder.Services.AddControllers().AddApplicationPart(typeof(ApiHost).Assembly);
        builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = ApiConfiguration.InvalidModelState);
        builder.Services.AddOpenApi();
        builder.Services.AddSignalR();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("GitCosmos") ?? "Data Source=gitcosmos.db");
        var app = builder.Build();
        app.UseMiddleware<RequestLanguageMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<LocalOriginMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapOpenApi();
        app.MapScalarApiReference();
        app.MapControllers();
        app.MapFallbackToFile("index.html");
        return app;
    }
}
