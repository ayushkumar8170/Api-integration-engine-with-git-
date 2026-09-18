using System.Text.Json;
using IntegrationEngine.Models;

namespace IntegrationEngine.Services;

/// <summary>
/// Validates an inbound payload against a named JSON Schema contract.
/// </summary>
public interface IPayloadValidator
{
    Task<ValidationResult> ValidateAsync(string schemaId, JsonElement payload, CancellationToken cancellationToken);
}
