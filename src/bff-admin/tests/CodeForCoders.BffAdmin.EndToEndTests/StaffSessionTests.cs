using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffSessionTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(StaffSession_LoginStoresAnOpaqueSessionAndEmitsTheContractCookie))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_LoginStoresAnOpaqueSessionAndEmitsTheContractCookie()
    {
        ResetState();
        using var client = factory.CreateClient();

        var login = await LoginAsync(client);

        Assert.Equal("Marina Alves", login.Session.Name);
        Assert.Equal(["administrador"], login.Session.Roles);
        Assert.Equal(["acesso.gerir"], login.Session.Permissions);
        Assert.False(string.IsNullOrWhiteSpace(login.Session.CsrfToken));
        Assert.StartsWith("staff_session=", login.Cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", login.SetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", login.SetCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("staff-sessions:create", factory.StaffSessionIdentityHandler.LastAssertionScope);
        Assert.Equal("staff-session-login", factory.StaffSessionIdentityHandler.LastIdempotencyKey);
        Assert.Equal("operator@example.com", factory.StaffSessionIdentityHandler.LastLoginRequest?.Email);
        Assert.Equal(1, factory.SessionStore.StoreCount);
    }

    [Fact(DisplayName = nameof(StaffSession_CurrentActionValidatesIdentityAndReturnsCurrentPermissions))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_CurrentActionValidatesIdentityAndReturnsCurrentPermissions()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = new StaffSessionValidatedV1(
            login.Session.AccountId,
            login.Session.Name,
            ["financeiro"],
            ["financeiro.ler"],
            DateTimeOffset.UtcNow.AddMinutes(40),
            null);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-sessions/current");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["financeiro"], session?.Roles);
        Assert.Equal(["financeiro.ler"], session?.Permissions);
        Assert.Equal(login.Session.CsrfToken, session?.CsrfToken);
        Assert.Equal(1, factory.StaffSessionIdentityHandler.ValidationCount);
        Assert.Equal("staff-sessions:validate", factory.StaffSessionIdentityHandler.LastAssertionScope);
        Assert.Null(factory.StaffSessionIdentityHandler.LastValidationAudience);
        Assert.Equal(2, factory.SessionStore.StoreCount);
    }

    [Fact(DisplayName = nameof(StaffSession_InvalidCredentialsHaveOnePublicUnauthorizedResult))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_InvalidCredentialsHaveOnePublicUnauthorizedResult()
    {
        ResetState();
        factory.StaffSessionIdentityHandler.CreateStatus = HttpStatusCode.Unauthorized;
        factory.StaffSessionIdentityHandler.CreateCode = "INVALID_CREDENTIALS";
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("student@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "staff-session-invalid");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_CREDENTIALS", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("E-mail ou senha inválidos.", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(0, factory.SessionStore.StoreCount);
    }

    [Fact(DisplayName = nameof(StaffSession_RevokedIdentitySessionRemovesValkeyEntryAndCookie))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_RevokedIdentitySessionRemovesValkeyEntryAndCookie()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidateStatus = HttpStatusCode.Unauthorized;
        factory.StaffSessionIdentityHandler.ValidateCode = "SESSION_REQUIRED";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-sessions/current");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("SESSION_REQUIRED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, factory.SessionStore.RemoveCount);
        Assert.Null(await factory.SessionStore.GetAsync(
            ExtractCookieValue(login.Cookie), TestContext.Current.CancellationToken));
        Assert.Contains("max-age=0", response.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = nameof(StaffSession_IdentityFailureDoesNotRenewValkeyTtl))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_IdentityFailureDoesNotRenewValkeyTtl()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var storeCountAfterLogin = factory.SessionStore.StoreCount;
        factory.StaffSessionIdentityHandler.ValidateStatus = HttpStatusCode.ServiceUnavailable;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-sessions/current");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("IDENTITY_UNAVAILABLE", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(storeCountAfterLogin, factory.SessionStore.StoreCount);
        Assert.NotNull(await factory.SessionStore.GetAsync(
            ExtractCookieValue(login.Cookie), TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = nameof(StaffSession_WriteWithoutSessionBoundCsrfOrAllowedOriginIsRejected))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_WriteWithoutSessionBoundCsrfOrAllowedOriginIsRejected()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var missingProof = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/staff-sessions/current");
        missingProof.Headers.Add("Cookie", login.Cookie);
        missingProof.Headers.Add("Origin", "http://localhost:8081");
        missingProof.Headers.Add("Idempotency-Key", "logout-missing-proof");
        using var missingProofResponse = await client.SendAsync(missingProof, TestContext.Current.CancellationToken);

        using var missingOrigin = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/staff-sessions/current");
        missingOrigin.Headers.Add("Cookie", login.Cookie);
        missingOrigin.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        missingOrigin.Headers.Add("Idempotency-Key", "logout-missing-origin");
        using var missingOriginResponse = await client.SendAsync(missingOrigin, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, missingProofResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missingOriginResponse.StatusCode);
        Assert.Equal(0, factory.StaffSessionIdentityHandler.RevocationCount);
        Assert.Equal(0, factory.SessionStore.RemoveCount);
    }

    [Fact(DisplayName = nameof(StaffSession_LogoutRevokesIdentityAndRemovesTheOpaqueSession))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_LogoutRevokesIdentityAndRemovesTheOpaqueSession()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/staff-sessions/current");
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", "staff-session-logout");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("staff-sessions:revoke", factory.StaffSessionIdentityHandler.LastAssertionScope);
        Assert.Equal(1, factory.StaffSessionIdentityHandler.RevocationCount);
        Assert.Equal(1, factory.SessionStore.RemoveCount);
        Assert.Null(await factory.SessionStore.GetAsync(
            ExtractCookieValue(login.Cookie), TestContext.Current.CancellationToken));
        Assert.Contains("max-age=0", response.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);

        using var replay = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/staff-sessions/current");
        replay.Headers.Add("Idempotency-Key", "staff-session-logout");
        using var replayResponse = await client.SendAsync(replay, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, replayResponse.StatusCode);
        Assert.Equal(1, factory.StaffSessionIdentityHandler.RevocationCount);
    }

    [Fact(DisplayName = nameof(StaffSession_ProxyIsRefusedWhenCurrentValidationHasNoAccessToken))]
    [Trait("Layer", "BffAdmin staff session - EndToEnd")]
    public async Task StaffSession_ProxyIsRefusedWhenCurrentValidationHasNoAccessToken()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/proxy/anything");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, factory.StaffSessionIdentityHandler.ValidationCount);
        Assert.Equal("staff-sessions:validate", factory.StaffSessionIdentityHandler.LastAssertionScope);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "staff-session-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        Assert.NotNull(session);
        return new LoginResult(setCookie.Split(';', 2)[0], session, setCookie);
    }

    private static string ExtractCookieValue(string cookie)
        => cookie.Split('=', 2)[1];

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session, string SetCookie);
}
