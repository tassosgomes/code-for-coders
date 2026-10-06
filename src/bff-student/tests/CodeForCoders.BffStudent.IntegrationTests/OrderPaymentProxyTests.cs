using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Endpoints;
using CodeForCoders.BffStudent.Api.Extensions;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class OrderPaymentProxyTests
{
    private static readonly Guid OrderId = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(StartPaymentForwardsToCommerceAndReturnsPaymentRedirect))]
    public async Task StartPaymentForwardsToCommerceAndReturnsPaymentRedirect()
    {
        var redirectJson = JsonSerializer.Serialize(new
        {
            orderId = OrderId,
            kind = "checkout",
            paymentUrl = "https://checkout.stripe.com/c/pay/cs_test_payment_1",
            expiresAt = DateTimeOffset.UtcNow.AddHours(24)
        });

        var boundary = new OrderBoundaryHandler { Body = redirectJson, StatusCode = 200 };
        var identity = Identity();
        await using var app = await Server(boundary, identity);
        using var client = Client(app, csrf: true);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderId}/payment-session", null, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);

        Assert.Equal($"/internal/v1/orders/{OrderId}/payment-session", boundary.Path);
        Assert.Equal("commerce-student-jwt", boundary.Token);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        var root = doc.RootElement;
        Assert.Equal(OrderId, root.GetProperty("orderId").GetGuid());
        Assert.Equal("checkout", root.GetProperty("kind").GetString());
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_payment_1", root.GetProperty("paymentUrl").GetString());
        Assert.True(root.TryGetProperty("expiresAt", out _));

        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "commerce", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(StartPaymentPropagatesOrderNotPayableWhenCommerceReturns422))]
    public async Task StartPaymentPropagatesOrderNotPayableWhenCommerceReturns422()
    {
        var errorJson = JsonSerializer.Serialize(new { code = "ORDER_NOT_PAYABLE" });
        var boundary = new OrderBoundaryHandler { Body = errorJson, StatusCode = 422 };
        await using var app = await Server(boundary, Identity());
        using var client = Client(app, csrf: true);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderId}/payment-session", null, Cancellation);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("ORDER_NOT_PAYABLE", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(StartPaymentPropagatesProviderUnavailableWhenCommerceReturns503OrFails))]
    public async Task StartPaymentPropagatesProviderUnavailableWhenCommerceReturns503OrFails()
    {
        var errorJson = JsonSerializer.Serialize(new { code = "PAYMENT_PROVIDER_UNAVAILABLE" });
        var boundary = new OrderBoundaryHandler { Body = errorJson, StatusCode = 503 };
        await using var app = await Server(boundary, Identity());
        using var client = Client(app, csrf: true);

        using var response = await client.PostAsync($"/api/v1/orders/{OrderId}/payment-session", null, Cancellation);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("PAYMENT_PROVIDER_UNAVAILABLE", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(StartPaymentRejectsMissingCsrfOrAnonymous))]
    public async Task StartPaymentRejectsMissingCsrfOrAnonymous()
    {
        var boundary = new OrderBoundaryHandler { Body = "{}", StatusCode = 200 };
        await using var app = await Server(boundary, Identity());

        // Anonymous -> 401
        using var anonClient = app.GetTestClient();
        using var anonResp = await anonClient.PostAsync($"/api/v1/orders/{OrderId}/payment-session", null, Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // Missing CSRF -> 403
        using var noCsrfClient = Client(app, csrf: false);
        using var noCsrfResp = await noCsrfClient.PostAsync($"/api/v1/orders/{OrderId}/payment-session", null, Cancellation);
        Assert.Equal(HttpStatusCode.Forbidden, noCsrfResp.StatusCode);
    }

    private static HttpClient Client(WebApplication app, bool csrf = false)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque");
        if (csrf)
        {
            client.DefaultRequestHeaders.Add("Origin", "http://student.test");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "csrf");
        }
        return client;
    }

    private static Mock<IStudentSessionIdentityClient> Identity(int status = 200)
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentSessionValidatedResult(status, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "commerce-student-jwt"));
        return mock;
    }

    private static async Task<WebApplication> Server(OrderBoundaryHandler boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:BaseAddress"] = "http://commerce.test/",
            ["Commerce:SigningKeyId"] = "test-key",
            ["Commerce:SigningKeyBase64"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey())
        });
        builder.Services.AddCommerceClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IOrdersCommerceClient, OrdersCommerceClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddOptions<BffSecurityOptions>();
        builder.Services.Configure<StudentSpaCorsOptions>(o => o.AllowedOrigins = ["http://student.test"]);
        builder.Services.AddSingleton(TimeProvider.System);
        var store = new Mock<IBffSessionStore>();
        store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5)));
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object);
        builder.Services.AddSingleton(identity.Object);
        var app = builder.Build();
        app.UseStatusCodePages(context => Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
            extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_ERROR" }).ExecuteAsync(context.HttpContext));
        app.UseMiddleware<BffSecurityMiddleware>();
        app.MapOrdersEndpoints();
        await app.StartAsync(Cancellation);
        return app;
    }
}
