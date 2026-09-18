using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationEngine.Configuration;
using IntegrationEngine.Exceptions;
using IntegrationEngine.Models;
using Microsoft.Extensions.Options;

namespace IntegrationEngine.Services;

/// <summary>
/// Bridges validated payloads to downstream REST endpoints. Each call is
/// bounded by the target's configured TimeoutMs via a linked
/// CancellationTokenSource, so a slow or hanging downstream can never
/// silently consume the caller's whole latency budget — it fails fast
/// with a LatencyBudgetExceededException instead.
/// </summary>
public sealed class EndpointBridgeService : IEndpointBridge
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IntegrationOptions _options;
    private readonly ILogger<EndpointBridgeService> _logger;

    public EndpointBridgeService(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> options,
        ILogger<EndpointBridgeService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BridgeResult> BridgeAsync(string targetId, JsonElement payload, CancellationToken cancellationToken)
    {
        var target = _options.BridgeTargets.FirstOrDefault(t => t.Id == targetId);
        if (target is null)
        {
            throw new UnknownBridgeTargetException(targetId);
        }

        var client = _httpClientFactory.CreateClient(nameof(EndpointBridgeService));
        client.BaseAddress = new Uri(target.BaseUrl);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(target.TimeoutMs));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await client.PostAsJsonAsync(target.Path, payload, linkedCts.Token);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Downstream {TargetId} returned {StatusCode} after {ElapsedMs}ms",
                    targetId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

                throw new UpstreamBridgeException(
                    targetId,
                    (int)response.StatusCode,
                    $"Downstream '{targetId}' returned status {(int)response.StatusCode}.");
            }

            return new BridgeResult
            {
                TargetId = targetId,
                Success = true,
                DownstreamStatusCode = (int)response.StatusCode,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Downstream {TargetId} exceeded its {TimeoutMs}ms timeout (budget {BudgetMs}ms)",
                targetId, target.TimeoutMs, _options.LatencyBudgetMs);

            throw new LatencyBudgetExceededException(targetId, _options.LatencyBudgetMs);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Transport failure bridging to {TargetId}", targetId);
            throw new UpstreamBridgeException(targetId, null, $"Failed to reach downstream '{targetId}': {ex.Message}");
        }
    }
}
