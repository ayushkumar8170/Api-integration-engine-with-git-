using IntegrationEngine.Configuration;
using IntegrationEngine.Middleware;
using IntegrationEngine.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<IntegrationOptions>(
    builder.Configuration.GetSection(IntegrationOptions.SectionName));
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));

// Core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient(nameof(EndpointBridgeService));
builder.Services.AddSingleton<IPayloadValidator, JsonSchemaPayloadValidator>();
builder.Services.AddSingleton<IEndpointBridge, EndpointBridgeService>();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Central exception handler — must be first so it wraps everything below,
// including auth failures if ApiKeyAuthMiddleware itself ever throws.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
