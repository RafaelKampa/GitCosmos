using System.Diagnostics;

namespace GitCosmos.API.Middlewares;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string ErrorCodeItemKey = "__request_logging_error_code";

    public async Task InvokeAsync(HttpContext context)
    {
        var watch = Stopwatch.StartNew();
        var correlationId = ReadId(context, "X-Correlation-ID", context.TraceIdentifier);
        var journeyId = ReadId(context, "X-Journey-ID", correlationId);
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        context.Response.Headers["X-Journey-ID"] = journeyId;
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = context.TraceIdentifier, ["CorrelationId"] = correlationId, ["JourneyId"] = journeyId
        });
        try { await next(context); }
        finally
        {
            logger.LogInformation("HTTP {Method} {Path} {StatusCode} in {ElapsedMilliseconds} ms. ErrorCode: {ErrorCode}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode,
                watch.Elapsed.TotalMilliseconds, context.Items[ErrorCodeItemKey]);
        }
    }

    private static string ReadId(HttpContext context, string header, string fallback)
    {
        var value = context.Request.Headers[header].FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
            value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or ':') ? value : fallback;
    }
}
