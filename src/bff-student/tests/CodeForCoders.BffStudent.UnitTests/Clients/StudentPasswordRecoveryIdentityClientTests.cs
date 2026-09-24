using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

public sealed class StudentPasswordRecoveryIdentityClientTests
{
    [Fact]
    public async Task RequestPasswordResetAsync_ReturnsAcceptedAndSendsRequestScopedAssertion()
    {
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.RequestPasswordResetAsync(
            new StudentPasswordResetRequestV1("ana@example.com"),
            "reset-request-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(202, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://identity.test/internal/v1/password-reset-requests", request.RequestUri?.ToString());
        Assert.Equal("reset-request-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("ana@example.com", body.RootElement.GetProperty("email").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-password-resets:request");
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsNoContentAndSendsExecuteScopedAssertion()
    {
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.ResetPasswordAsync(
            new StudentPasswordResetV1("reset-token", "NovaSenha1!"),
            "reset-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(204, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://identity.test/internal/v1/password-resets", request.RequestUri?.ToString());
        Assert.Equal("reset-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("reset-token", body.RootElement.GetProperty("token").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-password-resets:execute");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_REQUEST", 400, "INVALID_REQUEST")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "PASSWORD_RESET_REJECTED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Conflict, "IDEMPOTENCY_CONFLICT", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task RequestPasswordResetAsync_OnlyAcceptsItsOwnBusinessCodes(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.RequestPasswordResetAsync(
            new StudentPasswordResetRequestV1("ana@example.com"),
            "reset-request-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(expectedStatus, expectedCode), result);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity, "PASSWORD_RESET_REJECTED", 422, "PASSWORD_RESET_REJECTED")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "UNKNOWN_RULE", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.BadGateway, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task ResetPasswordAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.ResetPasswordAsync(
            new StudentPasswordResetV1("reset-token", "NovaSenha1!"),
            "reset-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(expectedStatus, expectedCode), result);
    }

    [Fact]
    public async Task ResetPasswordAsync_TreatsNonJsonErrorBodyAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.UnprocessableEntity, "not-json"));

        var result = await client.ResetPasswordAsync(
            new StudentPasswordResetV1("reset-token", "NovaSenha1!"),
            "reset-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task ResetPasswordAsync_ReportsUnavailableWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.ResetPasswordAsync(
            new StudentPasswordResetV1("reset-token", "NovaSenha1!"),
            "reset-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordRecoveryResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    private static StudentPasswordRecoveryIdentityClient CreateClient(FakeIdentityHandler handler)
        => new(IdentityClientTestSupport.CreateHttpClient(handler), IdentityClientTestSupport.CreateAssertionFactory());
}
