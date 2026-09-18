using System.Net;
using System.Text.Json;
using IntegrationEngine.Exceptions;
using IntegrationEngine.Models;

namespace IntegrationEngine.Middleware;

/// <summary>
/// Single place where every exception type the pipeline can throw gets
/// translated into a consistent ErrorEnvelope JSON shape with the
/// appropriate HTTP status code. Keeping this centralized means
/// controllers stay free of try/catch boilerplate and every consumer of
/// the API gets one predictable error contract.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (SchemaValidationException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, new ErrorEnvelope
            {
                Error = "schema_validation_failed",
                Message = ex.Message,
                Details = ex.ValidationErrors,
            });
        }
        catch (UnknownBridgeTargetException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.NotFound, new ErrorEnvelope
            {
                Error = "unknown_bridge_target",
                Message = ex.Message,
                TargetId = ex.TargetId,
            });
        }
        catch (LatencyBudgetExceededException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.GatewayTimeout, new ErrorEnvelope
            {
                Error = "latency_budget_exceeded",
                Message = ex.Message,
                TargetId = ex.TargetId,
            });
        }
        catch (UpstreamBridgeException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadGateway, new ErrorEnvelope
            {
                Error = "upstream_bridge_failed",
                Message = ex.Message,
                TargetId = ex.TargetId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, new ErrorEnvelope
            {
                Error = "internal_error",
                Message = "An unexpected error occurred while processing the request.",
            });
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, ErrorEnvelope envelope)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(envelope, SerializerOptions));
    }
}
