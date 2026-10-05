using Microsoft.AspNetCore.Http;
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
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
namespace CodeForCoders.BffStudent.IntegrationTests;

public sealed class OrderProxyTests
{
    private static readonly Guid CourseId = Guid.CreateVersion7();
    private static readonly Guid OfferId = Guid.CreateVersion7();
    private static readonly Guid OrderId = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact(DisplayName = nameof(SummaryRequestsCommerceAudienceAndForwardsBuyerBearer))]
    public async Task SummaryRequestsCommerceAudienceAndForwardsBuyerBearer()
    {
        var boundary = new OrderBoundaryHandler { Body = Summary() }; var identity = Identity();
        await using var app = await Server(boundary, identity); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/offers/{OfferId}/purchase-summary", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.Private); Assert.True(response.Headers.CacheControl.NoStore);
        Assert.Equal($"/internal/v1/offers/{OfferId}/purchase-summary", boundary.Path); Assert.Equal("commerce-student-jwt", boundary.Token);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), "commerce", It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory(DisplayName = nameof(CreationPreservesStatusPayloadKeyAndPublicLocation))]
    [InlineData(201)]
    [InlineData(200)]
    public async Task CreationPreservesStatusPayloadKeyAndPublicLocation(int status)
    {
        var boundary = new OrderBoundaryHandler { Body = Order(), StatusCode = status };
        await using var app = await Server(boundary, Identity()); using var client = Client(app, csrf: true);
        using var response = await Create(client);
        Assert.Equal(status, (int)response.StatusCode); Assert.Equal("confirmation-key", boundary.Key); Assert.Equal("commerce-student-jwt", boundary.Token);
        using var body = JsonDocument.Parse(boundary.RequestBody!); Assert.Single(body.RootElement.EnumerateObject()); Assert.Equal(OfferId, body.RootElement.GetProperty("offerId").GetGuid());
        Assert.Equal(status == 201 ? $"/api/v1/orders/{OrderId}" : null, response.Headers.Location?.ToString());
    }
    [Fact(DisplayName = nameof(OrderReadsUseCommerceAndReturnSnapshot))]
    public async Task OrderReadsUseCommerceAndReturnSnapshot()
    {
        var boundary = new OrderBoundaryHandler { Body = Order() }; await using var app = await Server(boundary, Identity()); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/orders/{OrderId}", Cancellation); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"/internal/v1/orders/{OrderId}", boundary.Path); Assert.Equal("commerce-student-jwt", boundary.Token); Assert.True(response.Headers.CacheControl!.NoStore);
    }
    [Fact(DisplayName = nameof(AnonymousBuyerRequiresSessionBeforeCallingServices))]
    public async Task AnonymousBuyerRequiresSessionBeforeCallingServices()
    {
        var boundary = new OrderBoundaryHandler(); var identity = Identity(); await using var app = await Server(boundary, identity); using var client = app.GetTestClient();
        using var response = await Create(client); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); await AssertCode(response, "SESSION_REQUIRED"); Assert.Null(boundary.Path);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact(DisplayName = nameof(MissingCsrfCannotCreateOrder))]
    public async Task MissingCsrfCannotCreateOrder()
    {
        var boundary = new OrderBoundaryHandler(); var identity = Identity(); await using var app = await Server(boundary, identity); using var client = Client(app);
        using var response = await Create(client); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); await AssertCode(response, "CSRF_INVALID"); Assert.Null(boundary.Path);
        identity.Verify(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory(DisplayName = nameof(RecognizedCommerceErrorsHavePublicCodesOnly))]
    [InlineData(404, "OFFER_NOT_AVAILABLE", "OFFER_NOT_AVAILABLE")]
    [InlineData(404, "ORDER_NOT_FOUND", "ORDER_NOT_FOUND")]
    [InlineData(422, "IDEMPOTENCY_KEY_REUSED", "IDEMPOTENCY_KEY_REUSED")]
    [InlineData(400, "INVALID_REQUEST", "VALIDATION_ERROR")]
    public async Task RecognizedCommerceErrorsHavePublicCodesOnly(int status, string internalCode, string publicCode)
    {
        var boundary = new OrderBoundaryHandler { StatusCode = status, Body = JsonSerializer.Serialize(new { code = internalCode, detail = "secret" }) };
        await using var app = await Server(boundary, Identity()); using var client = Client(app, csrf: true);
        using var response = await Create(client); Assert.Equal(status, (int)response.StatusCode); await AssertCode(response, publicCode); Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync(Cancellation));
    }
    [Theory(DisplayName = nameof(InvalidCommerceResponseFailsClosed))]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{not-json")]
    public async Task InvalidCommerceResponseFailsClosed(string body)
    {
        await using var app = await Server(new() { Body = body }, Identity()); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/orders/{OrderId}", Cancellation); Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode); await AssertCode(response, "COMMERCE_UNAVAILABLE");
    }
    [Fact(DisplayName = nameof(CommerceTimeoutReturns504))]
    public async Task CommerceTimeoutReturns504()
    {
        await using var app = await Server(new() { Timeout = true }, Identity()); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/orders/{OrderId}", Cancellation); Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode); await AssertCode(response, "UPSTREAM_TIMEOUT");
    }
    [Theory(DisplayName = nameof(IdentityFailureDoesNotCallCommerce))]
    [InlineData(502, "IDENTITY_UNAVAILABLE")]
    [InlineData(504, "UPSTREAM_TIMEOUT")]
    public async Task IdentityFailureDoesNotCallCommerce(int status, string code)
    {
        var boundary = new OrderBoundaryHandler(); await using var app = await Server(boundary, Identity(status)); using var client = Client(app);
        using var response = await client.GetAsync($"/api/v1/orders/{OrderId}", Cancellation); Assert.Equal(status, (int)response.StatusCode); await AssertCode(response, code); Assert.Null(boundary.Path);
    }

    [Theory(DisplayName = nameof(MalformedOrEnrichedCreationBodyIsValidationError))]
    [InlineData("{not-json")]
    [InlineData("{\"offerId\":\"00000000-0000-7000-8000-000000000001\",\"priceCents\":1}")]
    public async Task MalformedOrEnrichedCreationBodyIsValidationError(string body)
    {
        var boundary = new OrderBoundaryHandler(); await using var app = await Server(boundary, Identity()); using var client = Client(app, csrf: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", "body-check"); using var response = await client.SendAsync(request, Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); await AssertCode(response, "VALIDATION_ERROR"); Assert.Null(boundary.Path);
    }
    private static string Summary() => JsonSerializer.Serialize(new { course = new { courseId = CourseId, title = "C#" }, offer = new { offerId = OfferId, name = "12 months", priceCents = 49700, currency = "BRL", accessPeriod = new { type = "months", months = 12 } }, pendingOrderId = (Guid?)null, existingAccessChecked = true, existingAccess = (object?)null });
    private static string Order() => JsonSerializer.Serialize(new { orderId = OrderId, number = "000001", status = "awaiting-payment", course = new { courseId = CourseId, title = "C#" }, offer = new { offerId = OfferId, name = "12 months" }, priceCents = 49700, currency = "BRL", accessPeriod = new { type = "months", months = 12 }, paymentMethod = (string?)null, pendingPayment = (object?)null, paymentPageExpiresAt = (string?)null, accessGrantedAt = (string?)null, createdAt = DateTimeOffset.UtcNow, paidAt = (string?)null, expiredAt = (string?)null, cancelledAt = (string?)null });
    private static Mock<IStudentSessionIdentityClient> Identity(int status = 200)
    {
        var mock = new Mock<IStudentSessionIdentityClient>();
        mock.Setup(i => i.ValidateSessionAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new StudentSessionValidatedResult(status, null, Guid.CreateVersion7(), "Student", DateTimeOffset.UtcNow.AddMinutes(5), "commerce-student-jwt")); return mock;
    }
    private static HttpClient Client(WebApplication app, bool csrf = false)
    { var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("Cookie", "student_session=opaque"); if (csrf) { client.DefaultRequestHeaders.Add("Origin", "http://student.test"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "csrf"); } return client; }
    private static async Task<HttpResponseMessage> Create(HttpClient client)
    { using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(new { offerId = OfferId }) }; request.Headers.Add("Idempotency-Key", "confirmation-key"); return await client.SendAsync(request, Cancellation); }
    private static async Task AssertCode(HttpResponseMessage response, string code)
    { using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)); Assert.Equal(code, body.RootElement.GetProperty("code").GetString()); }
    private static async Task<WebApplication> Server(OrderBoundaryHandler boundary, Mock<IStudentSessionIdentityClient> identity)
    {
        using var key = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Commerce:BaseAddress"] = "http://commerce.test/", ["Commerce:SigningKeyId"] = "test-key", ["Commerce:SigningKeyBase64"] = Convert.ToBase64String(key.ExportPkcs8PrivateKey()) });
        builder.Services.AddCommerceClientConfiguration(builder.Configuration);
        builder.Services.AddHttpClient<IOrdersCommerceClient, OrdersCommerceClient>().ConfigurePrimaryHttpMessageHandler(() => boundary);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddOptions<BffSecurityOptions>(); builder.Services.Configure<StudentSpaCorsOptions>(o => o.AllowedOrigins = ["http://student.test"]); builder.Services.AddSingleton(TimeProvider.System);
        var store = new Mock<IBffSessionStore>(); store.Setup(s => s.GetAsync("opaque", It.IsAny<CancellationToken>())).ReturnsAsync(new OpaqueBffSession(Guid.CreateVersion7(), Guid.CreateVersion7(), "Student", "csrf", DateTimeOffset.UtcNow.AddMinutes(5)));
        store.Setup(s => s.StoreAsync(It.IsAny<string>(), It.IsAny<OpaqueBffSession>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(store.Object); builder.Services.AddSingleton(identity.Object);
        var app = builder.Build();
        app.UseStatusCodePages(context => Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
            extensions: new Dictionary<string, object?> { ["code"] = "VALIDATION_ERROR" }).ExecuteAsync(context.HttpContext));
        app.UseMiddleware<BffSecurityMiddleware>(); app.MapOrdersEndpoints(); await app.StartAsync(Cancellation); return app;
    }
}
