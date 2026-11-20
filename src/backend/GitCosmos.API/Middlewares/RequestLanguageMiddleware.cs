using GitCosmos.Application.Common.Localization;

namespace GitCosmos.API.Middlewares;

public sealed class RequestLanguageMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        RequestLanguage.Current = context.Request.Headers.AcceptLanguage.ToString();
        context.Response.Headers.ContentLanguage = RequestLanguage.Current;
        return next(context);
    }
}
