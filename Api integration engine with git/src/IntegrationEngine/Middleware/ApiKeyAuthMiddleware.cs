using System.Net;
using System.Text.Json;
using IntegrationEngine.Configuration;
using IntegrationEngine.Models;
using Microsoft.Extensions.Options;

namespace IntegrationEngine.Middleware;

/// <summary>
/// Minimal shared-secret authorization check for every /api/* request.
/// Requests must carry a matching X-Api-Key header; missing or
/// mismatched keys are rejected with 401 before the request reaches
/// validation or bridging, so unauthenticated callers can't burn
/// latency budget or trigger downstream calls.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly AuthOptions _options;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ApiKeyAuthMiddleware(RequestDelegate next, IOptions<AuthOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isApiRoute = context.Request.Path.StartsWithSegments("/api");
        var isHealthCheck = context.Request.Path.StartsWithSegments("/api/payloads/health");

        if (!_options.Enabled || !isApiRoute || isHealthCheck)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            string.IsNullOrEmpty(_options.ApiKey) ||
            providedKey != _options.ApiKey)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new ErrorEnvelope
            {
                Error = "unauthorized",
                Message = $"Missing or invalid {HeaderName} header.",
            }, SerializerOptions));
            return;
        }

        await _next(context);
    }
}
