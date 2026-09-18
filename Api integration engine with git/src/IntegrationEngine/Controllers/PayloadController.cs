using System.Diagnostics;
using IntegrationEngine.Exceptions;
using IntegrationEngine.Models;
using IntegrationEngine.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationEngine.Controllers;

[ApiController]
[Route("api/payloads")]
public sealed class PayloadController : ControllerBase
{
    private readonly IPayloadValidator _validator;
    private readonly IEndpointBridge _bridge;
    private readonly ILogger<PayloadController> _logger;

    public PayloadController(IPayloadValidator validator, IEndpointBridge bridge, ILogger<PayloadController> logger)
    {
        _validator = validator;
        _bridge = bridge;
        _logger = logger;
    }

    /// <summary>
    /// Validates the given payload against its schema and, if valid,
    /// bridges it to the requested downstream target. The whole
    /// operation is subject to the request's cancellation token, which
    /// ASP.NET Core ties to the client disconnect / the middleware's
    /// overall request budget.
    /// </summary>
    [HttpPost("ingest")]
    [ProducesResponseType(typeof(BridgeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Ingest([FromBody] PayloadEnvelope envelope, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var validation = await _validator.ValidateAsync(envelope.SchemaId, envelope.Payload, cancellationToken);
        if (!validation.IsValid)
        {
            throw new SchemaValidationException(validation.Errors);
        }

        var result = await _bridge.BridgeAsync(envelope.TargetId, envelope.Payload, cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation(
            "Ingest for target {TargetId} completed in {ElapsedMs}ms (downstream {DownstreamMs}ms)",
            envelope.TargetId, stopwatch.ElapsedMilliseconds, result.ElapsedMs);

        return Ok(new
        {
            status = "bridged",
            targetId = result.TargetId,
            elapsedMs = stopwatch.ElapsedMilliseconds,
            downstreamStatusCode = result.DownstreamStatusCode,
        });
    }

    /// <summary>Lightweight liveness/readiness probe for orchestrators and Compose healthchecks.</summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok(new { status = "healthy" });
}
