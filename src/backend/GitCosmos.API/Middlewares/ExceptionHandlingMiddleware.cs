using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCosmos.API.Middlewares;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller disconnected; stop work without writing an error to a closed response.
            context.Items[RequestLoggingMiddleware.ErrorCodeItemKey] = "request_cancelled";
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception exception)
        {
            var error = exception switch
            {
                AppException app => new ErrorDetails(app.StatusCode, app.ErrorCode, app.Errors),
                DbUpdateException { InnerException: SqliteException { SqliteExtendedErrorCode: 2067 or 1555 } } =>
                    new ErrorDetails(409, "conflict", [LocalizedText.Get("error.conflict")]),
                BadHttpRequestException => new ErrorDetails(400, "bad_request", [LocalizedText.Get("error.bad_request")]),
                UnauthorizedAccessException => new ErrorDetails(401, "unauthorized", [LocalizedText.Get("error.unauthorized")]),
                _ => new ErrorDetails(500, "internal_server_error", [LocalizedText.Get("error.internal")])
            };
            context.Items[RequestLoggingMiddleware.ErrorCodeItemKey] = error.ErrorCode;
            if (error.StatusCode >= 500)
                logger.LogError(exception, "Request failed with {ErrorCode}. TraceId: {TraceId}", error.ErrorCode, context.TraceIdentifier);
            context.Response.StatusCode = error.StatusCode;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(false, error.Errors, error.ErrorCode, context.TraceIdentifier));
        }
    }

    private sealed record ErrorDetails(int StatusCode, string ErrorCode, IReadOnlyCollection<string> Errors);
    public sealed record ErrorResponse(bool Success, IReadOnlyCollection<string> Errors, string ErrorCode, string TraceId);
}
