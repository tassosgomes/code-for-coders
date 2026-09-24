using System.Net;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

public sealed class StudentPasswordChangeIdentityClientTests
{
    [Fact]
    public async Task ChangePasswordAsync_SendsSessionBoundRequestWithChangeScopedAssertion()
    {
        var sessionId = Guid.CreateVersion7();
        var handler = FakeIdentityHandler.Returning(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.ChangePasswordAsync(
            sessionId,
            new StudentPasswordChangeV1("SenhaAtual1!", "NovaSenha1!"),
            "change-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordChangeResult(204, null), result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://identity.test/internal/v1/password-changes", request.RequestUri?.ToString());
        Assert.Equal("change-key", request.IdempotencyKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(sessionId, body.RootElement.GetProperty("sessionId").GetGuid());
        Assert.Equal("SenhaAtual1!", body.RootElement.GetProperty("currentPassword").GetString());
        Assert.Equal("NovaSenha1!", body.RootElement.GetProperty("newPassword").GetString());
        IdentityClientTestSupport.AssertServiceAssertion(request, "student-password-changes:execute");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_REQUEST", 400, "INVALID_REQUEST")]
    [InlineData(HttpStatusCode.Unauthorized, "SESSION_REQUIRED", 401, "SESSION_REQUIRED")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "PASSWORD_CHANGE_REJECTED", 422, "PASSWORD_CHANGE_REJECTED")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_CONFLICT", 422, "IDEMPOTENCY_CONFLICT")]
    [InlineData(HttpStatusCode.Unauthorized, "SERVICE_UNAUTHORIZED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Conflict, "PASSWORD_CHANGE_REJECTED", 502, "IDENTITY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "INTERNAL", 502, "IDENTITY_UNAVAILABLE")]
    public async Task ChangePasswordAsync_MapsIdentityErrorsToPublicOutcome(
        HttpStatusCode identityStatus,
        string identityCode,
        int expectedStatus,
        string expectedCode)
    {
        var client = CreateClient(FakeIdentityHandler.Returning(identityStatus, new { code = identityCode }));

        var result = await client.ChangePasswordAsync(
            Guid.CreateVersion7(),
            new StudentPasswordChangeV1("SenhaAtual1!", "NovaSenha1!"),
            "change-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordChangeResult(expectedStatus, expectedCode), result);
    }

    [Fact]
    public async Task ChangePasswordAsync_TreatsNonJsonErrorBodyAsIdentityUnavailable()
    {
        var client = CreateClient(FakeIdentityHandler.ReturningRaw(HttpStatusCode.Unauthorized, "unauthorized"));

        var result = await client.ChangePasswordAsync(
            Guid.CreateVersion7(),
            new StudentPasswordChangeV1("SenhaAtual1!", "NovaSenha1!"),
            "change-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordChangeResult(502, "IDENTITY_UNAVAILABLE"), result);
    }

    [Theory]
    [MemberData(nameof(IdentityClientTestSupport.TransportFailures), MemberType = typeof(IdentityClientTestSupport))]
    public async Task ChangePasswordAsync_ReportsUnavailableWhenIdentityIsUnreachable(string failure, int expectedStatus)
    {
        var client = CreateClient(FakeIdentityHandler.Throwing(IdentityClientTestSupport.CreateTransportFailure(failure)));

        var result = await client.ChangePasswordAsync(
            Guid.CreateVersion7(),
            new StudentPasswordChangeV1("SenhaAtual1!", "NovaSenha1!"),
            "change-key",
            TestContext.Current.CancellationToken);

        Assert.Equal(new StudentPasswordChangeResult(expectedStatus, "IDENTITY_UNAVAILABLE"), result);
    }

    [Fact]
    public void ServiceAssertionTokenFactory_RefusesScopeNotConfiguredForTheBff()
    {
        var factory = new ServiceAssertionTokenFactory(
            Options.Create(new StudentIdentityOptions { Scope = "student-sessions:validate" }),
            TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => factory.Create("student-password-changes:execute"));
    }

    private static StudentPasswordChangeIdentityClient CreateClient(FakeIdentityHandler handler)
        => new(IdentityClientTestSupport.CreateHttpClient(handler), IdentityClientTestSupport.CreateAssertionFactory());
}
