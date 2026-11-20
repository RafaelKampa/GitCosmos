using System.Text.Json;
using FluentValidation;
using GitCosmos.API;
using GitCosmos.API.Controllers;
using GitCosmos.API.Extensions;
using GitCosmos.API.Middlewares;
using GitCosmos.Application;
using GitCosmos.Application.Common;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Responses.System;
using GitCosmos.Application.Interfaces.Services;
using GitCosmos.Application.Services;
using GitCosmos.Application.Validators;
using GitCosmos.Domain.Common;
using GitCosmos.Infrastructure;
using GitCosmos.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GitCosmos.Application.Tests;

public class FoundationTests
{
    [Theory]
    [InlineData(null, "pt-BR")]
    [InlineData("", "pt-BR")]
    [InlineData("de", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("ES", "es")]
    [InlineData("es-PY,en;q=0.9", "es")]
    [InlineData("EN-us", "en")]
    public async Task RequestLanguage_NormalizesAndLocalizesStatus(string? language, string expected)
    {
        RequestLanguage.Current = language!;
        var result = await new SystemService().GetStatusAsync();
        Assert.True(result.Success);
        Assert.Equal(expected, result.Result!.Language);
        Assert.Equal("GitCosmos", result.Result.Name);
        Assert.Equal("0.1.0-dev", result.Result.Version);
        Assert.Single(result.Messages);
        Assert.Equal(expected, RequestLanguage.Normalize(language));
        Assert.Contains("field", SetEnglishAndFormat());
    }

    private static string SetEnglishAndFormat()
    {
        RequestLanguage.Current = "en";
        return LocalizedText.Get("model.invalid_value", "field");
    }

    [Fact]
    public async Task Language_IsIsolatedAcrossConcurrentRequests()
    {
        var results = await Task.WhenAll(new[] { "pt-BR", "es", "en" }.Select(async language =>
        {
            RequestLanguage.Current = language;
            await Task.Yield();
            Assert.Equal(language, RequestLanguage.Current);
            return LocalizedText.Get("system.ready");
        }));
        Assert.Equal(3, results.Distinct().Count());
    }

    [Fact]
    public async Task Service_PropagatesCancellation()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SystemService().GetStatusAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task Controller_ReturnsServiceEnvelopeAndToken()
    {
        using var source = new CancellationTokenSource();
        var envelope = CustomResponse<SystemStatusResponse>.Ok(new("GitCosmos", "test", "en"));
        var service = new Mock<ISystemService>();
        service.Setup(x => x.GetStatusAsync(source.Token)).ReturnsAsync(envelope);
        var result = await new SystemController(service.Object).GetStatus(source.Token);
        Assert.Same(envelope, Assert.IsType<OkObjectResult>(result).Value);
        service.VerifyAll();
    }

    [Fact]
    public void CustomResponse_PreservesSuccessFailureAndListContracts()
    {
        var empty = new CustomResponse<string>();
        Assert.False(empty.Success);
        Assert.Empty(empty.Errors);
        Assert.Empty(empty.Messages);
        empty.Success = true; empty.Result = "value"; empty.TotalRows = 1;
        empty.Errors = ["error"]; empty.Messages = ["message"];
        Assert.Equal("value", empty.Result);
        Assert.Equal(1, empty.TotalRows);
        Assert.Single(empty.Errors); Assert.Single(empty.Messages);
        Assert.True(CustomResponse<string>.Ok("ok").Success);
        Assert.Empty(CustomResponse<string>.Ok("ok").Errors);
        Assert.Equal("done", Assert.Single(CustomResponse<string>.Ok("ok", "done").Messages));
        Assert.Equal("invalid", Assert.Single(CustomResponse<string>.Fail("invalid").Errors));
        Assert.Equal("context", CustomResponse<string>.Fail(["error"], "context").Result);
        var list = CustomResponse<string>.OkList(["a", "b"], 12);
        Assert.True(list.Success); Assert.Equal(12, list.TotalRows); Assert.Equal(2, list.Result!.Count);
        Assert.Empty(CustomResponse<string>.FailList("error").Result!);
        Assert.Single(CustomResponse<string>.FailList(["error"]).Errors);
        Assert.False(CustomResponse<string>.FailList(["error"]).Success);
        var json = JsonSerializer.Serialize(CustomResponse<string>.Ok("ok"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("totalRows", json);
        Assert.Contains("\"success\":true", json);
    }

    [Fact]
    public void Pagination_UsesZeroBasedPagesAndReturnsTotalPages()
    {
        var defaults = new PaginationRequest();
        Assert.Equal(0, defaults.Page ?? 0); Assert.Null(defaults.PageSize);
        Assert.Equal(0, defaults.GetPage()); Assert.Equal(10, defaults.GetPageSize());
        var normalized = new PaginationRequest(2, 3).Normalize();
        Assert.Equal(2, normalized.Page); Assert.Equal(3, normalized.PageSize);
        Assert.Equal(new PaginationRequest(0, 10), defaults.Normalize());
        var result = new PagedResult<string>(["a"], 7, 2, 3);
        Assert.Equal(3, result.TotalPages); Assert.Equal(7, result.TotalRows);
        Assert.Equal(2, result.Page); Assert.Equal(3, result.PageSize); Assert.Single(result.Items);
        var empty = new PagedResult<string> { Page = 0, PageSize = 10, TotalRows = 0, Items = [] };
        Assert.Equal(0, empty.TotalPages); Assert.Empty(empty.Items);
    }

    [Fact]
    public void Validation_ThrowsApplicationExceptionWithAllMessages()
    {
        var validator = new InlineValidator<string>();
        validator.RuleFor(x => x).NotEmpty().WithMessage("required");
        validator.ValidateAndThrowApp("valid");
        var error = Assert.Throws<ValidationAppException>(() => validator.ValidateAndThrowApp(""));
        Assert.Equal(400, error.StatusCode); Assert.Equal("required", Assert.Single(error.Errors));
    }

    public static TheoryData<AppException, int, string> ApplicationErrors => new()
    {
        { new ValidationAppException("invalid"), 400, "validation_error" },
        { new ValidationAppException(["first", "second"]), 400, "validation_error" },
        { new ValidationAppException(Array.Empty<string>()), 400, "validation_error" },
        { new AuthenticationFailedAppException("invalid"), 401, "authentication_failed" },
        { new AccessDeniedAppException("invalid"), 403, "access_denied" },
        { new NotFoundAppException("invalid"), 404, "not_found" },
        { new ConflictAppException("invalid"), 409, "conflict" },
        { new OperationFailedAppException("invalid"), 500, "operation_failed" },
        { new ExternalServiceAppException(["remote"], 502, "remote_failure"), 502, "remote_failure" }
    };

    [Theory]
    [MemberData(nameof(ApplicationErrors))]
    public async Task ExceptionMiddleware_UsesApplicationErrorContract(AppException exception, int status, string code)
    {
        var body = await InvokeError(exception, status, code);
        Assert.Equal(exception.Errors, body.GetProperty("errors").EnumerateArray().Select(x => x.GetString()));
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("es")]
    [InlineData("en")]
    public async Task ExceptionMiddleware_LocalizesUnexpectedAndKnownFrameworkErrors(string language)
    {
        RequestLanguage.Current = language;
        var unexpected = await InvokeError(new Exception("secret"), 500, "internal_server_error");
        Assert.DoesNotContain("secret", unexpected.ToString());
        Assert.Equal(LocalizedText.Get("error.internal"), unexpected.GetProperty("errors")[0].GetString());
        await InvokeError(new BadHttpRequestException("secret"), 400, "bad_request");
        await InvokeError(new UnauthorizedAccessException("secret"), 401, "unauthorized");
        await InvokeError(new DbUpdateException("db", new SqliteException("secret", 19, 2067)), 409, "conflict");
        await InvokeError(new DbUpdateException("db", new SqliteException("secret", 19, 1555)), 409, "conflict");
        await InvokeError(new DbUpdateException("db", new SqliteException("secret", 19, 787)), 500, "internal_server_error");
    }

    private static async Task<JsonElement> InvokeError(Exception exception, int status, string code)
    {
        var context = Context();
        var middleware = new ExceptionHandlingMiddleware(_ => Task.FromException(exception), NullLogger<ExceptionHandlingMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, context.Items[RequestLoggingMiddleware.ErrorCodeItemKey]);
        var body = JsonDocument.Parse(ReadBody(context)).RootElement.Clone();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("errorCode").GetString());
        Assert.Equal("trace-1", body.GetProperty("traceId").GetString());
        Assert.False(body.TryGetProperty("result", out _));
        return body;
    }

    [Fact]
    public async Task Middleware_AllowsSuccessfulRequests()
    {
        var context = Context();
        var called = false;
        await new ExceptionHandlingMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<ExceptionHandlingMiddleware>.Instance).InvokeAsync(context);
        Assert.True(called); Assert.Equal(200, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("ES")]
    [InlineData("en")]
    public async Task LanguageMiddleware_SetsLanguageAndBindingErrors(string language)
    {
        var context = Context();
        context.Request.Headers.AcceptLanguage = language;
        await new RequestLanguageMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        var expected = RequestLanguage.Normalize(language);
        Assert.Equal(expected, context.Response.Headers.ContentLanguage);
        var modelState = new ModelStateDictionary();
        modelState.SetModelValue("valid", "ok", "ok");
        modelState.AddModelError("id", "technical error");
        var action = new ActionContext(context, new RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor(), modelState);
        var result = Assert.IsType<BadRequestObjectResult>(ApiConfiguration.InvalidModelState(action));
        var error = Assert.IsType<ExceptionHandlingMiddleware.ErrorResponse>(result.Value);
        Assert.False(error.Success); Assert.Equal("validation_error", error.ErrorCode); Assert.Equal("trace-1", error.TraceId);
        Assert.Equal(LocalizedText.Get("model.invalid_value", "id"), Assert.Single(error.Errors));
    }

    [Theory]
    [InlineData(null, "trace-1")]
    [InlineData("", "trace-1")]
    [InlineData("valid-id_1.a:b", "valid-id_1.a:b")]
    [InlineData("bad value", "trace-1")]
    public async Task Logging_ValidatesCorrelationIds(string? supplied, string expected)
    {
        var context = Context();
        if (supplied is not null) context.Request.Headers["X-Correlation-ID"] = supplied;
        context.Request.Headers["X-Journey-ID"] = "journey-1";
        await new RequestLoggingMiddleware(_ => Task.CompletedTask, NullLogger<RequestLoggingMiddleware>.Instance).InvokeAsync(context);
        Assert.Equal(expected, context.Response.Headers["X-Correlation-ID"]);
        Assert.Equal("journey-1", context.Response.Headers["X-Journey-ID"]);
    }

    [Fact]
    public async Task Logging_RejectsOversizedIdsAndPreservesFailures()
    {
        var context = Context();
        context.Request.Headers["X-Correlation-ID"] = new string('x', 101);
        var middleware = new RequestLoggingMiddleware(_ => throw new InvalidOperationException(), NullLogger<RequestLoggingMiddleware>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Equal("trace-1", context.Response.Headers["X-Journey-ID"]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("http://127.0.0.1:5210", true)]
    [InlineData("http://evil.test", false)]
    [InlineData("null", false)]
    public async Task LocalOrigin_RejectsCrossOriginBrowserRequests(string? origin, bool allowed)
    {
        var context = Context(); context.Request.Host = new HostString("127.0.0.1", 5210);
        if (origin is not null) context.Request.Headers.Origin = origin;
        var called = false;
        var middleware = new LocalOriginMiddleware(_ => { called = true; return Task.CompletedTask; });
        if (allowed) { await middleware.InvokeAsync(context); Assert.True(called); }
        else await Assert.ThrowsAsync<AccessDeniedAppException>(async () => await middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task Host_ComposesControllersServicesAndSqlite()
    {
        foreach (var args in new[] { Array.Empty<string>(), new[] { "--ConnectionStrings:GitCosmos=Data Source=:memory:" }, new[] { $"--contentRoot={Path.GetTempPath()}" } })
        {
            await using var app = ApiHost.Build(args);
            using var scope = app.Services.CreateScope();
            Assert.IsType<SystemService>(scope.ServiceProvider.GetRequiredService<ISystemService>());
            Assert.NotNull(app.Services.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value.InvalidModelStateResponseFactory);
            var db = scope.ServiceProvider.GetRequiredService<GitCosmosDbContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
        }
        var services = new ServiceCollection().AddApplication().AddInfrastructure("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var serviceScope = provider.CreateScope();
        Assert.NotNull(serviceScope.ServiceProvider.GetRequiredService<GitCosmosDbContext>());
    }

    private static DefaultHttpContext Context()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1", RequestServices = services };
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }
}
