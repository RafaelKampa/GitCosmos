using GitCosmos.API.Middlewares;
using GitCosmos.Application.Common.Localization;
using Microsoft.AspNetCore.Mvc;

namespace GitCosmos.API.Extensions;

public static class ApiConfiguration
{
    public static IActionResult InvalidModelState(ActionContext context)
    {
        var errors = context.ModelState.Where(x => x.Value!.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(_ => LocalizedText.Get("model.invalid_value", x.Key))).ToList();
        return new BadRequestObjectResult(new ExceptionHandlingMiddleware.ErrorResponse(
            false, errors, "validation_error", context.HttpContext.TraceIdentifier));
    }
}
