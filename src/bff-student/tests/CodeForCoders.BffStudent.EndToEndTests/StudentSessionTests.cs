using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.ApiModels;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

[Collection(BffStudentApiCollection.Name)]
public sealed class StudentSessionTests(BffStudentApiFactory factory)
{
    private const string SessionCookieName = "student_session";

    [Fact]
    public async Task StudentSession_LoginIssuesOpaqueCookieAndReturnsOnlyIdentityAndCsrfProof()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sessionId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200,
            null,
            sessionId,
            accountId,
            "Ana Souza",
            DateTimeOffset.FromUnixTimeSeconds(253402300000));
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student-sessions")
        {
            Content = JsonContent.Create(new { email = "ana@example.com", password = "SenhaForte1!" }),
        };
        request.Headers.Add("Idempotency-Key", "login-key");
        using var response = await client.SendAsync(request, cancellationToken);
        var bodyJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = JsonSerializer.Deserialize<StudentSessionResponse>(
            bodyJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        var cookieValue = GetCookieValue(setCookie);
        var stored = factory.SessionStore.GetStored(cookieValue);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accountId, body?.AccountId);
        Assert.Equal("Ana Souza", body?.Name);
        Assert.False(string.IsNullOrWhiteSpace(body?.CsrfToken));
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sessionId.ToString("D"), cookieValue, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", bodyJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sessionId, stored?.StudentSessionId);
        Assert.Equal(body?.CsrfToken, stored?.CsrfToken);
        Assert.Equal("login-key", factory.StudentSessionClient.LoginIdempotencyKey);
    }

    [Fact]
    public async Task StudentSession_CurrentSessionValidatesWithIdentityAndRenewsOpaqueState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sessionId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200,
            null,
            sessionId,
            accountId,
            "Ana Souza",
            DateTimeOffset.FromUnixTimeSeconds(253402300000));
        factory.StudentSessionClient.ValidateResult = new StudentSessionValidatedResult(
            200,
            null,
            accountId,
            "Ana Souza Atualizada",
            DateTimeOffset.MaxValue);
        var validationCallsBefore = factory.StudentSessionClient.ValidationCalls;
        using var client = factory.CreateClient();
        var cookieValue = await LoginAsync(client, cancellationToken);
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionCookieName}={cookieValue}");

        using var response = await client.GetAsync("/api/v1/student-sessions/current", cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<StudentSessionResponse>(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(sessionId, factory.StudentSessionClient.ValidatedSessionId);
        Assert.Equal(validationCallsBefore + 1, factory.StudentSessionClient.ValidationCalls);
        Assert.Equal(accountId, body?.AccountId);
        Assert.Equal("Ana Souza Atualizada", body?.Name);
        Assert.Equal(factory.SessionStore.GetStored(cookieValue)?.CsrfToken, body?.CsrfToken);
        Assert.Equal(DateTimeOffset.MaxValue, factory.SessionStore.GetStored(cookieValue)?.ExpiresAt);
        Assert.Null(factory.StudentSessionClient.ValidatedAudience);
    }

    [Fact]
    public async Task StudentSession_CsrfProofFromAnotherSessionBlocksLogoutBeforeIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200,
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ana Souza",
            DateTimeOffset.MaxValue);
        var revocationCallsBefore = factory.StudentSessionClient.RevocationCalls;
        using var client = factory.CreateClient();
        var cookieValue = await LoginAsync(client, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/student-sessions/current");
        request.Headers.Add("Cookie", $"{SessionCookieName}={cookieValue}");
        request.Headers.Add("Idempotency-Key", "logout-cross-session");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("X-CSRF-Token", "proof-from-another-session");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CSRF_INVALID", body.GetProperty("code").GetString());
        Assert.Equal(revocationCallsBefore, factory.StudentSessionClient.RevocationCalls);
        Assert.NotNull(factory.SessionStore.GetStored(cookieValue));
    }

    [Fact]
    public async Task StudentSession_LogoutRevokesOnlyCurrentOpaqueSessionAndClearsCookie()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var firstSessionId = Guid.CreateVersion7();
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200,
            null,
            firstSessionId,
            Guid.CreateVersion7(),
            "Ana Souza",
            DateTimeOffset.MaxValue);
        using var firstClient = factory.CreateClient();
        var firstCookie = await LoginAsync(firstClient, cancellationToken);
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200,
            null,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Ana Souza",
            DateTimeOffset.MaxValue);
        using var secondClient = factory.CreateClient();
        var secondCookie = await LoginAsync(secondClient, cancellationToken);

        firstClient.DefaultRequestHeaders.Add("Cookie", $"{SessionCookieName}={firstCookie}");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/student-sessions/current");
        request.Headers.Add("Idempotency-Key", "logout-first");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("X-CSRF-Token", factory.SessionStore.GetStored(firstCookie)!.CsrfToken);
        using var response = await firstClient.SendAsync(request, cancellationToken);
        var clearedCookie = response.Headers.GetValues("Set-Cookie").Single();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(firstSessionId, factory.StudentSessionClient.RevokedSessionId);
        Assert.Equal("logout-first", factory.StudentSessionClient.RevocationIdempotencyKey);
        Assert.Null(factory.SessionStore.GetStored(firstCookie));
        Assert.NotNull(factory.SessionStore.GetStored(secondCookie));
        Assert.Contains("Max-Age=0", clearedCookie, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> LoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/student-sessions")
        {
            Content = JsonContent.Create(new { email = "ana@example.com", password = "SenhaForte1!" }),
        };
        request.Headers.Add("Idempotency-Key", "login-key");
        using var response = await client.SendAsync(request, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return GetCookieValue(response.Headers.GetValues("Set-Cookie").Single());
    }

    private static string GetCookieValue(string cookieHeader)
        => cookieHeader.Split(';', 2)[0].Split('=', 2)[1];
}
