using System.Text.Json;
using System.Text.Json.Serialization;

namespace IntegrationEngine.Models;

/// <summary>
/// The inbound request body for POST /api/payloads/ingest.
/// </summary>
public sealed class PayloadEnvelope
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = string.Empty;

    [JsonPropertyName("schemaId")]
    public string SchemaId { get; set; } = "payload.schema.json";

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}

/// <summary>
/// Result of validating a payload against its JSON Schema contract.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Result of bridging a validated payload to a downstream endpoint.
/// </summary>
public sealed class BridgeResult
{
    public required string TargetId { get; init; }
    public required bool Success { get; init; }
    public int? DownstreamStatusCode { get; init; }
    public long ElapsedMs { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Standard error response shape used by ExceptionHandlingMiddleware and
/// by controllers for expected failure cases (validation, unknown target).
/// </summary>
public sealed class ErrorEnvelope
{
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("details")]
    public IReadOnlyList<string>? Details { get; init; }

    [JsonPropertyName("targetId")]
    public string? TargetId { get; init; }
}
