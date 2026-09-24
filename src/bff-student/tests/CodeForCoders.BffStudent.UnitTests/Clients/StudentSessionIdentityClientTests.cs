using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

public sealed class StudentSessionIdentityClientTests
{
    private static readonly DateTimeOffset ExpiresAt = DateTimeOffset.Parse(
        "2026-09-23T13:00:00Z",
        System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task CreateSessionAsync_ReturnsSessionAndSendsCreateScopedAssertionWithIdempotencyKey()
    {
        var sessionId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var handler = FakeIdentityHandler.Returning(
            HttpStatusCode.OK,
            new StudentSessionCreatedV1(sessionId, accountId, "Ana Souza", ExpiresAt));
        var client = CreateClient(handler);

        var result = await client.CreateSessionAsync(
            new StudentSessionLoginV1("ana@example.com", "SenhaForte1!"),
            "login-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionCreatedResult(200, null, sessionId, accountId, "Ana Souza", ExpiresAt), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://identity.test/internal/v1/student-sessions", request.RequestUri?.ToString());
        Assert.Equal("login-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("ana@example.com", body.RootElement.GetProperty("email").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-sessions:create");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS", 401, "INVALID_CREDENTIALS")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "EMAIL_NOT_CONFIRMED", 422, "EMAIL_NOT_CONFIRMED")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_REQUEST", 400, "INVALID_REQUEST")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Conflict, "ACCOUNT_ALREADY_EXISTS", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task CreateSessionAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.CreateSessionAsync(
            new StudentSessionLoginV1("ana@example.com", "errada"),
            "login-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionCreatedResult(expectedStatus, expectedCode), result);
    }

    [Fact]
    public async Task CreateSessionAsync_TreatsNullSuccessBodyAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.OK, "null"));

        var result = await client.CreateSessionAsync(
            new StudentSessionLoginV1("ana@example.com", "SenhaForte1!"),
            "login-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionCreatedResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Fact]
    public async Task CreateSessionAsync_TreatsNonJsonErrorBodyAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.Unauthorized, "<html>proxy</html>"));

        var result = await client.CreateSessionAsync(
            new StudentSessionLoginV1("ana@example.com", "SenhaForte1!"),
            "login-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionCreatedResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task CreateSessionAsync_ClosesAccessWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.CreateSessionAsync(
            new StudentSessionLoginV1("ana@example.com", "SenhaForte1!"),
            "login-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionCreatedResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    [Fact]
    public async Task ValidateSessionAsync_ReturnsIdentityAndSendsValidateScopedAssertionWithoutIdempotencyKey()
    {
        var sessionId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        var handler = FakeIdentityHandler.Returning(
            HttpStatusCode.OK,
            new StudentSessionValidatedV1(accountId, "Ana Souza", ExpiresAt, "internal-jwt"));
        var client = CreateClient(handler);

        var result = await client.ValidateSessionAsync(sessionId, "learning", TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionValidatedResult(200, null, accountId, "Ana Souza", ExpiresAt, "internal-jwt"), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://identity.test/internal/v1/student-session-validations", request.RequestUri?.ToString());
        Assert.Null(request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(sessionId, body.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("learning", body.RootElement.GetProperty("audience").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-sessions:validate");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "SESSION_REQUIRED", 401, "SESSION_REQUIRED")]
    [InlineData(HttpStatusCode.Forbidden, "AUDIENCE_NOT_ALLOWED", 403, "AUDIENCE_NOT_ALLOWED")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task ValidateSessionAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.ValidateSessionAsync(Guid.CreateVersion7(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionValidatedResult(expectedStatus, expectedCode), result);
    }

    [Fact]
    public async Task ValidateSessionAsync_TreatsNullSuccessBodyAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.OK, "null"));

        var result = await client.ValidateSessionAsync(Guid.CreateVersion7(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionValidatedResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task ValidateSessionAsync_ClosesAccessWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.ValidateSessionAsync(Guid.CreateVersion7(), null, TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionValidatedResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    [Fact]
    public async Task RevokeSessionAsync_SendsRevokeScopedAssertionAndReturnsNoContent()
    {
        var sessionId = Guid.CreateVersion7();
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.RevokeSessionAsync(sessionId, "logout-key", TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionRevokedResult(204, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://identity.test/internal/v1/student-session-revocations", request.RequestUri?.ToString());
        Assert.Equal("logout-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(sessionId, body.RootElement.GetProperty("sessionId").GetGuid());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-sessions:revoke");
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.BadGateway, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task RevokeSessionAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.RevokeSessionAsync(Guid.CreateVersion7(), "logout-key", TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionRevokedResult(expectedStatus, expectedCode), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task RevokeSessionAsync_ReportsUnavailableWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.RevokeSessionAsync(Guid.CreateVersion7(), "logout-key", TestContext.Current.CancellationToken);

        Assert.Equal(new StudentSessionRevokedResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    private static StudentSessionIdentityClient CreateClient(FakeIdentityHandler handler)
        => new(IdentityClientTestSupport.CreateHttpClient(handler), IdentityClientTestSupport.CreateAssertionFactory());
}
