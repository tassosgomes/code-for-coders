using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class StudentPasswordRecoveryTests(BffStudentApiFactory factory)
{
    [Fact]
    public async Task StudentPasswordRecoveryRequest_ReturnsTheSameNeutralResponseForEligibleAndUnknownEmail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentPasswordRecoveryClient.RequestResult = new(StatusCodes.Status202Accepted, null);
        using var client = factory.CreateClient();
        using var eligibleRequest = CreateRequest(
            "/api/v1/password-reset-requests",
            new StudentPasswordResetRequestV1("ana@example.com"),
            "reset-request-eligible");
        using var unknownRequest = CreateRequest(
            "/api/v1/password-reset-requests",
            new StudentPasswordResetRequestV1("unknown@example.com"),
            "reset-request-unknown");

        using var eligibleResponse = await client.SendAsync(eligibleRequest, cancellationToken);
        var eligibleBody = await eligibleResponse.Content.ReadAsStringAsync(cancellationToken);
        using var unknownResponse = await client.SendAsync(unknownRequest, cancellationToken);
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, eligibleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.Equal(eligibleBody, unknownBody);
        Assert.Empty(eligibleBody);
        Assert.Equal(2, factory.StudentPasswordRecoveryClient.RequestCalls);
        Assert.Equal(new StudentPasswordResetRequestV1("unknown@example.com"), factory.StudentPasswordRecoveryClient.Request);
        Assert.Equal("reset-request-unknown", factory.StudentPasswordRecoveryClient.RequestIdempotencyKey);
    }

    [Fact]
    public void StudentPasswordRecoveryIdentityClient_ResolvesWithRegisteredServiceAssertionFactory()
    {
        using var scope = factory.Services.CreateScope();

        var assertionFactory = scope.ServiceProvider.GetRequiredService<ServiceAssertionTokenFactory>();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = ActivatorUtilities.CreateInstance<StudentPasswordRecoveryIdentityClient>(
            scope.ServiceProvider,
            httpClientFactory.CreateClient());

        Assert.NotNull(client);
        Assert.NotEmpty(assertionFactory.Create("student-password-resets:request"));
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_ForwardsTokenAndPasswordAndReturnsNoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentPasswordRecoveryClient.ResetResult = new(StatusCodes.Status204NoContent, null);
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/api/v1/password-resets",
            new StudentPasswordResetV1("single-use-token", "NovaSenha2!"),
            "reset-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new StudentPasswordResetV1("single-use-token", "NovaSenha2!"), factory.StudentPasswordRecoveryClient.Reset);
        Assert.Equal("reset-1", factory.StudentPasswordRecoveryClient.ResetIdempotencyKey);
    }

    [Fact]
    public async Task StudentPasswordRecoveryReset_HidesWhetherTheTokenOrPasswordWasRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentPasswordRecoveryClient.ResetResult = new(
            StatusCodes.Status422UnprocessableEntity,
            "PASSWORD_RESET_REJECTED");
        using var client = factory.CreateClient();
        using var request = CreateRequest(
            "/api/v1/password-resets",
            new StudentPasswordResetV1("invalid-token", "NovaSenha2!"),
            "reset-2");

        using var response = await client.SendAsync(request, cancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<RecoveryProblem>(cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("PASSWORD_RESET_REJECTED", problem!.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task StudentPasswordRecovery_RejectsNextProtectedActionWhenIdentityRevokesTheSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cookieValue = "opaque-session-after-reset";
        var sessionId = Guid.CreateVersion7();
        var session = new OpaqueBffSession(
            sessionId,
            Guid.CreateVersion7(),
            "Ana Souza",
            "csrf-proof",
            TimeProvider.System.GetUtcNow().AddMinutes(30));
        await factory.SessionStore.StoreAsync(cookieValue, session, cancellationToken);
        var previousValidation = factory.StudentSessionClient.ValidateResult;
        try
        {
            factory.StudentSessionClient.ValidateResult = new(StatusCodes.Status401Unauthorized, "SESSION_REQUIRED");
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/student-sessions/current");
            request.Headers.Add("Cookie", $"student_session={cookieValue}");

            using var response = await client.SendAsync(request, cancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(sessionId, factory.StudentSessionClient.ValidatedSessionId);
            Assert.Null(await factory.SessionStore.GetAsync(cookieValue, cancellationToken));
            Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("student_session=", StringComparison.Ordinal));
        }
        finally
        {
            factory.StudentSessionClient.ValidateResult = previousValidation;
        }
    }

    private static HttpRequestMessage CreateRequest<TBody>(string path, TBody body, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record RecoveryProblem(string Code, string TraceId);
}
