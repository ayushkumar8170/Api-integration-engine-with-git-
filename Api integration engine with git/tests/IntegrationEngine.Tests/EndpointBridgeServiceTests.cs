using System.Net;
using System.Text.Json;
using IntegrationEngine.Configuration;
using IntegrationEngine.Exceptions;
using IntegrationEngine.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace IntegrationEngine.Tests;

public class EndpointBridgeServiceTests
{
    private static IntegrationOptions BuildOptions(int targetTimeoutMs = 100) => new()
    {
        LatencyBudgetMs = 130,
        BridgeTargets = new List<BridgeTargetOptions>
        {
            new()
            {
                Id = "orders-service",
                BaseUrl = "http://stub-downstream.local",
                Path = "/ingest",
                TimeoutMs = targetTimeoutMs,
            },
        },
    };

    private static IHttpClientFactory BuildFactory(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factoryMock.Object;
    }

    [Fact]
    public async Task SuccessfulDownstreamCall_ReturnsSuccessResult()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var service = new EndpointBridgeService(
            BuildFactory(handlerMock.Object),
            Options.Create(BuildOptions()),
            NullLogger<EndpointBridgeService>.Instance);

        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1", amount = 1, currency = "USD" });
        var result = await service.BridgeAsync("orders-service", payload, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.DownstreamStatusCode);
    }

    [Fact]
    public async Task UnknownTarget_ThrowsUnknownBridgeTargetException()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var service = new EndpointBridgeService(
            BuildFactory(handlerMock.Object),
            Options.Create(BuildOptions()),
            NullLogger<EndpointBridgeService>.Instance);

        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1", amount = 1, currency = "USD" });

        await Assert.ThrowsAsync<UnknownBridgeTargetException>(
            () => service.BridgeAsync("does-not-exist", payload, CancellationToken.None));
    }

    [Fact]
    public async Task DownstreamExceedsTimeout_ThrowsLatencyBudgetExceededException()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage _, CancellationToken ct) =>
            {
                // Simulate a slow downstream that outlives the configured timeout.
                await Task.Delay(500, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        // Target timeout much shorter than the simulated delay above.
        var service = new EndpointBridgeService(
            BuildFactory(handlerMock.Object),
            Options.Create(BuildOptions(targetTimeoutMs: 20)),
            NullLogger<EndpointBridgeService>.Instance);

        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1", amount = 1, currency = "USD" });

        await Assert.ThrowsAsync<LatencyBudgetExceededException>(
            () => service.BridgeAsync("orders-service", payload, CancellationToken.None));
    }

    [Fact]
    public async Task DownstreamErrorStatusCode_ThrowsUpstreamBridgeException()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var service = new EndpointBridgeService(
            BuildFactory(handlerMock.Object),
            Options.Create(BuildOptions()),
            NullLogger<EndpointBridgeService>.Instance);

        var payload = JsonSerializer.SerializeToElement(new { orderId = "ORD-1", amount = 1, currency = "USD" });

        await Assert.ThrowsAsync<UpstreamBridgeException>(
            () => service.BridgeAsync("orders-service", payload, CancellationToken.None));
    }
}
