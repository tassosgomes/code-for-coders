using System.Net;
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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class MyOrdersProxyTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(ListForwardsPaginationAndCommerceBuyerBearerAndPreservesPayload))]
    public async Task ListForwardsPaginationAndCommerceBuyerBearerAndPreservesPayload()
    {
        var body = Page();
        var boundary = new OrderBoundaryHandler { Body = body };
        var identity = Identity();
        await using var app = await ServerAsync(boundary, identity);
        using var client = Client(app);
        using var response = await client.GetAsync("/api/v1/orders?_page=2&_size=1&studentId=ignored&tenantId=ignored", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/internal/v1/orders", boundary.Path);
        Assert.Equal("?_page=2&_size=1", boundary.Query);
        Assert.Equal("commerce-student-jwt", boundary.Token);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(Cancellation));
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.True(response.Headers.CacheControl.NoStore);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "commerce", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = nameof(DefaultPaginationAndEmptyListArePreserved))]
    public async Task DefaultPaginationAndEmptyListArePreserved()
    {
        var boundary = new OrderBoundaryHandler { Body = JsonSerializer.Serialize(new { data = Array.Empty<object>(), pagination = new { page = 1, size = 10, total = 0, totalPages = 0 } }) };
        await using var app = await ServerAsync(boundary, Identity());
        using var client = Client(app);
        using var response = await client.GetAsync("/api/v1/orders", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?_page=1&_size=10", boundary.Query);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Empty(body.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact(DisplayName = nameof(AnonymousListRequiresSessionBeforeCallingIdentityOrCommerce))]
    public async Task AnonymousListRequiresSessionBeforeCallingIdentityOrCommerce()
    {
        var boundary = new OrderBoundaryHandler();
        var identity = Identity();
        await using var app = await ServerAsync(boundary, identity);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/v1/orders", Cancellation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertCodeAsync(response, "SESSION_REQUIRED");
        Assert.Null(boundary.Path);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory(DisplayName = nameof(InvalidPaginationDoesNotCallCommerce))]
    [InlineData("_page=0")]
    [InlineData("_size=51")]
    [InlineData("_size=0")]
    [InlineData("_page=2147483647&_size=50")]
    public async Task InvalidPaginationDoesNotCallCommerce(string query)
    {
        var boundary = new OrderBoundaryHandler();
        await using var app = await ServerAsync(boundary, Identity());
        using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/orders?{query}", Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertCodeAsync(response, "VALIDATION_ERROR");
        Assert.Null(boundary.Path);
    }

    [Theory(DisplayName = nameof(MalformedListFailsClosed))]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{not-json")]
    [InlineData("{\"data\":[null],\"pagination\":{\"page\":1,\"size\":10,\"total\":1,\"totalPages\":1}}")]
    [InlineData("{\"data\":[],\"pagination\":{\"page\":1,\"size\":0,\"total\":0,\"totalPages\":0}}")]
    public async Task MalformedListFailsClosed(string body)
    {
        await using var app = await ServerAsync(new() { Body = body }, Identity());
        using var client = Client(app);
        using var response = await client.GetAsync("/api/v1/orders", Cancellation);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        await AssertCodeAsync(response, "COMMERCE_UNAVAILABLE");
    }

    [Fact(DisplayName = nameof(CommerceTimeoutReturns504))]
    public async Task CommerceTimeoutReturns504()
    {
        await using var app = await ServerAsync(new() { Timeout = true }, Identity());
        using var client = Client(app);
        using var response = await client.GetAsync("/api/v1/orders", Cancellation);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        await AssertCodeAsync(response, "UPSTREAM_TIMEOUT");
    }

    private static string Page() => JsonSerializer.Serialize(new
    {
        data = new[] { new
        {
            orderId = Guid.CreateVersion7(), number = "000123", status = "paid",
            course = new { courseId = Guid.CreateVersion7(), title = "Frozen course" },
            offer = new { offerId = Guid.CreateVersion7(), name = "12 months" }, priceCents = 49700, currency = "BRL",
            accessPeriod = new { type = "months", months = 12 }, paymentMethod = "card", pendingPayment = (object?)null,
            paymentPageExpiresAt = (string?)null, accessGrantedAt = DateTimeOffset.UtcNow, createdAt = DateTimeOffset.UtcNow,
            paidAt = DateTimeOffset.UtcNow, expiredAt = (string?)null, cancelledAt = (string?)null,
        } },
        pagination = new { page = 2, size = 1, total = 3, totalPages = 3 },
    });

    private static Mock<IStudentSessionIdentityClient> Identity()
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StudentSessionValidatedResult(200, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "commerce-student-jwt"));
        return mock;
    }

    private static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque");
        return client;
    }

    private static async Task AssertCodeAsync(HttpResponseMessage response, string code)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    private static async Task<WebApplication> ServerAsync(OrderBoundaryHandler boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:BaseAddress"] = "http://commerce.test/",
            ["Commerce:SigningKeyId"] = "test-key",
            ["Commerce:SigningKeyBase64"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
        });
        builder.Services.AddCommerceClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IOrdersCommerceClient, OrdersCommerceClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddOptions<BffSecurityOptions>();
        builder.Services.Configure<StudentSpaCorsOptions>(options => options.AllowedOrigins = ["http://student.test"]);
        builder.Services.AddSingleton(TimeProvider.System);
        var store = new Mock<IBffSessionStore>();
        store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5)));
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object);
        builder.Services.AddSingleton(identity.Object);
        var app = builder.Build();
        app.UseMiddleware<BffSecurityMiddleware>();
        app.MapOrdersEndpoints();
        await app.StartAsync(Cancellation);
        return app;
    }
}
