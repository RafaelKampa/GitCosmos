using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;

namespace GitCosmos.API.Middlewares;

// Browser clients use the same-origin API (Vite proxies during development).
public sealed class LocalOriginMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase)))
            throw new AccessDeniedAppException(LocalizedText.Get("error.origin"));
        return next(context);
    }
}
