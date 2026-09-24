using System.Net;
using System.Net.Http.Json;
using CodeForCoders.BffStudent.Application.Common;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class StudentPasswordChangeTests(BffStudentApiFactory factory)
{
    [Fact]
    public async Task StudentPasswordChange_ForwardsTheValidatedSessionAndPasswordsToIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cookieValue = "opaque-password-change-session";
        var sessionId = Guid.CreateVersion7();
        var csrfToken = "password-change-csrf-proof";
        await factory.SessionStore.StoreAsync(
            cookieValue,
            new OpaqueBffSession(
                sessionId,
                Guid.CreateVersion7(),
                "Ana Souza",
                csrfToken,
                DateTimeOffset.MaxValue),
            cancellationToken);
        factory.StudentPasswordChangeClient.Result = new(StatusCodes.Status204NoContent, null);
        var callsBefore = factory.StudentPasswordChangeClient.Calls;
        using var client = factory.CreateClient();
        using var request = CreateRequest(cookieValue, csrfToken, "change-password-1");

        using var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(callsBefore + 1, factory.StudentPasswordChangeClient.Calls);
        Assert.Equal(sessionId, factory.StudentPasswordChangeClient.SessionId);
        Assert.Equal(new StudentPasswordChangeV1("SenhaLegada", "NovaSenha2!"), factory.StudentPasswordChangeClient.Request);
        Assert.Equal("change-password-1", factory.StudentPasswordChangeClient.IdempotencyKey);
        Assert.NotNull(factory.SessionStore.GetStored(cookieValue));
    }

    [Fact]
    public async Task StudentPasswordChange_RejectsAnInvalidCsrfProofBeforeCallingIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cookieValue = "opaque-password-change-invalid-csrf";
        await factory.SessionStore.StoreAsync(
            cookieValue,
            new OpaqueBffSession(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "Ana Souza",
                "correct-csrf-proof",
                DateTimeOffset.MaxValue),
            cancellationToken);
        var callsBefore = factory.StudentPasswordChangeClient.Calls;
        using var client = factory.CreateClient();
        using var request = CreateRequest(cookieValue, "csrf-from-another-session", "change-password-csrf-2");

        using var response = await client.SendAsync(request, cancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ChangePasswordProblem>(cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("CSRF_INVALID", problem!.Code);
        Assert.Equal(callsBefore, factory.StudentPasswordChangeClient.Calls);
        Assert.NotNull(factory.SessionStore.GetStored(cookieValue));
    }

    [Fact]
    public async Task StudentPasswordChange_ReturnsOnlyTheContractCodeWhenIdentityRejectsCredentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var cookieValue = "opaque-password-change-rejected";
        await factory.SessionStore.StoreAsync(
            cookieValue,
            new OpaqueBffSession(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "Ana Souza",
                "rejected-csrf-proof",
                DateTimeOffset.MaxValue),
            cancellationToken);
        factory.StudentPasswordChangeClient.Result = new(StatusCodes.Status422UnprocessableEntity, "PASSWORD_CHANGE_REJECTED");
        using var client = factory.CreateClient();
        using var request = CreateRequest(cookieValue, "rejected-csrf-proof", "change-password-rejected-3");

        using var response = await client.SendAsync(request, cancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ChangePasswordProblem>(cancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("PASSWORD_CHANGE_REJECTED", problem!.Code);
        Assert.DoesNotContain("SenhaLegada", problem.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("NovaSenha2!", problem.Title, StringComparison.Ordinal);
    }

    private static HttpRequestMessage CreateRequest(string cookieValue, string csrfToken, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/password-changes")
        {
            Content = JsonContent.Create(new StudentPasswordChangeV1("SenhaLegada", "NovaSenha2!")),
        };
        request.Headers.Add("Cookie", $"student_session={cookieValue}");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("X-CSRF-Token", csrfToken);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record ChangePasswordProblem(string Code, string Title);
}
