namespace IntegrationEngine.Exceptions;

/// <summary>
/// Thrown when an inbound payload fails JSON Schema validation.
/// Caught by ExceptionHandlingMiddleware and translated to a 400 response.
/// </summary>
public sealed class SchemaValidationException : Exception
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public SchemaValidationException(IReadOnlyList<string> validationErrors)
        : base("Payload does not conform to the configured schema.")
    {
        ValidationErrors = validationErrors;
    }
}

/// <summary>
/// Thrown when the request references a bridge target that isn't configured.
/// Translated to a 404 response.
/// </summary>
public sealed class UnknownBridgeTargetException : Exception
{
    public string TargetId { get; }

    public UnknownBridgeTargetException(string targetId)
        : base($"No bridge target configured with id '{targetId}'.")
    {
        TargetId = targetId;
    }
}

/// <summary>
/// Thrown when the ingest -> validate -> bridge round trip exceeds the
/// configured LatencyBudgetMs. Translated to a 504 response.
/// </summary>
public sealed class LatencyBudgetExceededException : Exception
{
    public string TargetId { get; }
    public int BudgetMs { get; }

    public LatencyBudgetExceededException(string targetId, int budgetMs)
        : base($"Downstream call exceeded the {budgetMs}ms latency budget.")
    {
        TargetId = targetId;
        BudgetMs = budgetMs;
    }
}

/// <summary>
/// Thrown when the downstream endpoint is reachable but returns a
/// non-success status code or a transport-level failure occurs.
/// Translated to a 502 response.
/// </summary>
public sealed class UpstreamBridgeException : Exception
{
    public string TargetId { get; }
    public int? StatusCode { get; }

    public UpstreamBridgeException(string targetId, int? statusCode, string message)
        : base(message)
    {
        TargetId = targetId;
        StatusCode = statusCode;
    }
}
