using GitCosmos.API.Middlewares;
using GitCosmos.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCosmos.Application.Tests;

public class RequestCancellationTests
{
    [Theory]
    [InlineData("127.0.0.1:5173", "http://127.0.0.1:5173", true)]
    [InlineData("localhost:5173", "http://localhost:5173", true)]
    [InlineData("127.0.0.1:5210", "http://127.0.0.1:5173", false)]
    [InlineData("127.0.0.1:5173", "https://external.example", false)]
    public async Task LocalOrigin_PreservesBrowserProtectionThroughDevelopmentProxy(string host, string origin, bool allowed)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Headers.Origin = origin;
        var called = false;
        var middleware = new LocalOriginMiddleware(_ => { called = true; return Task.CompletedTask; });
        if (allowed)
        {
            await middleware.InvokeAsync(context);
            Assert.True(called);
        }
        else
        {
            await Assert.ThrowsAsync<AccessDeniedAppException>(() => middleware.InvokeAsync(context));
            Assert.False(called);
        }
    }

    [Theory]
    [InlineData(true, 499)]
    [InlineData(false, 500)]
    public async Task Middleware_DistinguishesDisconnectedRequestsFromUnexpectedCancellation(bool disconnected, int status)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        using var source = new CancellationTokenSource();
        if (disconnected) source.Cancel();
        context.RequestAborted = source.Token;
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        if (disconnected)
        {
            Assert.Equal(0, body.Length);
            Assert.Equal("request_cancelled", context.Items[RequestLoggingMiddleware.ErrorCodeItemKey]);
        }
        else
        {
            Assert.True(body.Length > 0);
            Assert.Equal("internal_server_error", context.Items[RequestLoggingMiddleware.ErrorCodeItemKey]);
        }
    }
}
