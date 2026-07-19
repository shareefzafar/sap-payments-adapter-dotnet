using System.Net;
using System.Text.Json;

namespace SapPaymentsAdapter.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (KeyNotFoundException ex)
        {
            // Controllers throw this for "resource doesn't exist in SAP" -
            // a normal, expected outcome, not a SAP upstream failure.
            _logger.LogInformation("{Method} {Path} -> 404: {Message}", context.Request.Method, context.Request.Path, ex.Message);
            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;

            var notFoundPayload = new
            {
                type = "about:blank",
                title = "Resource not found",
                status = context.Response.StatusCode,
                detail = ex.Message,
                sapMessages = Array.Empty<object>(),
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(notFoundPayload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Method} {Path} -> unhandled exception", context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;

            var payload = new
            {
                type = "about:blank",
                title = "SAP upstream call failed",
                status = context.Response.StatusCode,
                detail = ex.Message,
                sapMessages = Array.Empty<object>(),
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<GlobalExceptionMiddleware>();
}