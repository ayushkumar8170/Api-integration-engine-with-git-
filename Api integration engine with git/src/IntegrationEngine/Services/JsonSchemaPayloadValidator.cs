using System.Collections.Concurrent;
using System.Text.Json;
using IntegrationEngine.Models;
using NJsonSchema;

namespace IntegrationEngine.Services;

/// <summary>
/// Validates payloads against JSON Schema files under the Schemas/
/// directory, using NJsonSchema. Compiled schemas are cached in-memory
/// per schemaId since re-parsing the schema on every request would be
/// wasted work under the service's latency budget.
/// </summary>
public sealed class JsonSchemaPayloadValidator : IPayloadValidator
{
    private readonly string _schemasDirectory;
    private readonly ConcurrentDictionary<string, Lazy<Task<JsonSchema>>> _schemaCache = new();
    private readonly ILogger<JsonSchemaPayloadValidator> _logger;

    public JsonSchemaPayloadValidator(IWebHostEnvironment env, ILogger<JsonSchemaPayloadValidator> logger)
    {
        _schemasDirectory = Path.Combine(env.ContentRootPath, "Schemas");
        _logger = logger;
    }

    public async Task<ValidationResult> ValidateAsync(string schemaId, JsonElement payload, CancellationToken cancellationToken)
    {
        JsonSchema schema;
        try
        {
            schema = await GetSchemaAsync(schemaId);
        }
        catch (FileNotFoundException)
        {
            return ValidationResult.Failure(new[] { $"Unknown schemaId '{schemaId}'." });
        }

        var errors = schema.Validate(payload.GetRawText());
        if (errors.Count == 0)
        {
            return ValidationResult.Success();
        }

        var messages = errors
            .Select(e => $"{e.Path}: {e.Kind} ({e.Property})")
            .ToList();

        _logger.LogWarning("Payload failed schema validation against {SchemaId}: {Errors}", schemaId, string.Join("; ", messages));
        return ValidationResult.Failure(messages);
    }

    private Task<JsonSchema> GetSchemaAsync(string schemaId)
    {
        var lazy = _schemaCache.GetOrAdd(schemaId, id => new Lazy<Task<JsonSchema>>(() => LoadSchemaAsync(id)));
        return lazy.Value;
    }

    private async Task<JsonSchema> LoadSchemaAsync(string schemaId)
    {
        var path = Path.Combine(_schemasDirectory, schemaId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Schema file not found: {schemaId}", path);
        }

        return await JsonSchema.FromFileAsync(path);
    }
}
