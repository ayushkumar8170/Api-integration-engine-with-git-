using System.Text.Json;
using IntegrationEngine.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace IntegrationEngine.Tests;

public class JsonSchemaPayloadValidatorTests
{
    private static JsonSchemaPayloadValidator CreateValidator()
    {
        // Content root points at the main project so the validator finds
        // the real Schemas/payload.schema.json shipped with the service.
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "IntegrationEngine");

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetFullPath(contentRoot));

        return new JsonSchemaPayloadValidator(envMock.Object, NullLogger<JsonSchemaPayloadValidator>.Instance);
    }

    [Fact]
    public async Task ValidPayload_PassesValidation()
    {
        var validator = CreateValidator();
        var payload = JsonSerializer.SerializeToElement(new
        {
            orderId = "ORD-1029",
            amount = 42.50,
            currency = "USD",
        });

        var result = await validator.ValidateAsync("payload.schema.json", payload, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task MissingRequiredField_FailsValidation()
    {
        var validator = CreateValidator();
        var payload = JsonSerializer.SerializeToElement(new
        {
            orderId = "ORD-1029",
            currency = "USD",
            // amount intentionally omitted
        });

        var result = await validator.ValidateAsync("payload.schema.json", payload, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task InvalidCurrencyFormat_FailsValidation()
    {
        var validator = CreateValidator();
        var payload = JsonSerializer.SerializeToElement(new
        {
            orderId = "ORD-1029",
            amount = 10,
            currency = "us-dollars", // does not match ^[A-Z]{3}$
        });

        var result = await validator.ValidateAsync("payload.schema.json", payload, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UnknownSchemaId_FailsValidationGracefully()
    {
        var validator = CreateValidator();
        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1029", amount = 1, currency = "USD" });

        var result = await validator.ValidateAsync("does-not-exist.schema.json", payload, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does-not-exist.schema.json"));
    }
}
