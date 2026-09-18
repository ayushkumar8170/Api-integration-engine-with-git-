using System.Text.Json;
using IntegrationEngine.Models;

namespace IntegrationEngine.Services;

/// <summary>
/// Forwards a validated payload to a configured downstream endpoint,
/// enforcing the configured latency budget.
/// </summary>
public interface IEndpointBridge
{
    Task<BridgeResult> BridgeAsync(string targetId, JsonElement payload, CancellationToken cancellationToken);
}
