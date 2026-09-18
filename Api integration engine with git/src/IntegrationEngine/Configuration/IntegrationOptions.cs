namespace IntegrationEngine.Configuration;

/// <summary>
/// Root configuration bound from the "Integration" section of appsettings.
/// </summary>
public sealed class IntegrationOptions
{
    public const string SectionName = "Integration";

    /// <summary>
    /// End-to-end latency budget, in milliseconds, for a single
    /// ingest -> validate -> bridge round trip. Enforced by
    /// EndpointBridgeService via a CancellationTokenSource timeout.
    /// </summary>
    public int LatencyBudgetMs { get; set; } = 130;

    /// <summary>
    /// Downstream endpoints the engine is allowed to bridge payloads to.
    /// </summary>
    public List<BridgeTargetOptions> BridgeTargets { get; set; } = new();
}

public sealed class BridgeTargetOptions
{
    public string Id { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Per-target timeout for the outbound HTTP call. Should stay under
    /// LatencyBudgetMs to leave headroom for validation/serialization.
    /// </summary>
    public int TimeoutMs { get; set; } = 100;
}

/// <summary>
/// Bound from the "Auth" section. A minimal shared-secret API key check
/// for inbound requests — swap ApiKeyAuthMiddleware for OAuth2/JWT
/// bearer auth if the deployment needs per-caller identity rather than
/// a single shared credential.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Expected value of the X-Api-Key header on every /api/* request.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>When false, ApiKeyAuthMiddleware is a no-op — useful for local dev.</summary>
    public bool Enabled { get; set; } = true;
}
