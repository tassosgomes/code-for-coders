using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

public sealed class StudentRegistrationIdentityClientTests
{
    [Fact]
    public async Task RegisterAsync_ReturnsAcceptedAndSendsCreateScopedAssertionWithIdempotencyKey()
    {
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.RegisterAsync(
            new StudentRegistrationRequestV1("Ana Souza", "ana@example.com", "SenhaForte1!"),
            "register-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentRegistrationResult(202, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://identity.test/internal/v1/student-accounts", request.RequestUri?.ToString());
        Assert.Equal("register-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("Ana Souza", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("ana@example.com", body.RootElement.GetProperty("email").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-accounts:create");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_REQUEST", 400, "INVALID_REQUEST")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "ACCOUNT_ALREADY_EXISTS", 422, "ACCOUNT_ALREADY_EXISTS")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "PASSWORD_POLICY_VIOLATION", 422, "PASSWORD_POLICY_VIOLATION")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "UNKNOWN_RULE", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Conflict, "ACCOUNT_ALREADY_EXISTS", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task RegisterAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.RegisterAsync(
            new StudentRegistrationRequestV1("Ana Souza", "ana@example.com", "SenhaForte1!"),
            "register-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentRegistrationResult(expectedStatus, expectedCode), result);
    }

    [Fact]
    public async Task RegisterAsync_TreatsErrorWithoutCodeAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.BadRequest, "{\"code\":42}"));

        var result = await client.RegisterAsync(
            new StudentRegistrationRequestV1("Ana Souza", "ana@example.com", "SenhaForte1!"),
            "register-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentRegistrationResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task RegisterAsync_ReportsUnavailableWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.RegisterAsync(
            new StudentRegistrationRequestV1("Ana Souza", "ana@example.com", "SenhaForte1!"),
            "register-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentRegistrationResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnsNoContentAndSendsConfirmScopedAssertion()
    {
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.ConfirmAsync(
            new StudentAccountConfirmationTokenV1("verification-token"),
            "confirm-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentConfirmationResult(204, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://identity.test/internal/v1/account-confirmations", request.RequestUri?.ToString());
        Assert.Equal("confirm-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("verification-token", body.RootElement.GetProperty("token").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-accounts:confirm");
    }

    [Fact]
    public async Task RequestConfirmationAsync_ReturnsAcceptedAndSendsRequestConfirmationScopedAssertion()
    {
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.Accepted);
        var client = CreateClient(handler);

        var result = await client.RequestConfirmationAsync(
            new StudentAccountConfirmationEmailV1("ana@example.com"),
            "resend-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentConfirmationResult(202, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://identity.test/internal/v1/account-confirmation-requests", request.RequestUri?.ToString());
        Assert.Equal("resend-key", request.IdempotencyKey);
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-accounts:request-confirmation");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_REQUEST", 400, "INVALID_REQUEST")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "INVALID_VERIFICATION_TOKEN", 422, "INVALID_VERIFICATION_TOKEN")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.Accepted, "UNEXPECTED_SUCCESS", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task ConfirmAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.ConfirmAsync(
            new StudentAccountConfirmationTokenV1("verification-token"),
            "confirm-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentConfirmationResult(expectedStatus, expectedCode), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task RequestConfirmationAsync_ReportsUnavailableWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.RequestConfirmationAsync(
            new StudentAccountConfirmationEmailV1("ana@example.com"),
            "resend-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentConfirmationResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    private static StudentRegistrationIdentityClient CreateClient(FakeIdentityHandler handler)
        => new(IdentityClientTestSupport.CreateHttpClient(handler), IdentityClientTestSupport.CreateAssertionFactory());
}
