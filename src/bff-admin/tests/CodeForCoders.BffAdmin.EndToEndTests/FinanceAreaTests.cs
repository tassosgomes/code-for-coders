using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class FinanceAreaTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(FinanceArea_RefusesProfessorBeforeCallingCommerce))]
    [Trait("Layer", "BffAdmin finance area - EndToEnd")]
    public async Task FinanceArea_RefusesProfessorBeforeCallingCommerce()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["professor"],
            ["autoria.ler"]);

        using var response = await GetFinanceAreaAsync(client, login.Cookie);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, factory.CommerceFinanceAreaHandler.RequestCount);
        Assert.Equal(1, factory.StaffSessionIdentityHandler.ValidationCount);
        Assert.Null(factory.StaffSessionIdentityHandler.LastValidationAudience);
    }

    [Fact(DisplayName = nameof(FinanceArea_RequestsCommerceAudienceAndOpensForFinanceActor))]
    [Trait("Layer", "BffAdmin finance area - EndToEnd")]
    public async Task FinanceArea_RequestsCommerceAudienceAndOpensForFinanceActor()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        const string accessToken = "staff-user-finance-token";
        factory.StaffSessionIdentityHandler.ValidatedSession = ValidatedSession(
            login.Session.AccountId,
            ["financeiro"],
            ["financeiro.ler"],
            accessToken);

        using var response = await GetFinanceAreaAsync(client, login.Cookie);
        var area = await response.Content.ReadFromJsonAsync<FinanceAreaResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reserved", area?.Status);
        Assert.Equal(2, factory.StaffSessionIdentityHandler.ValidationCount);
        Assert.Equal("commerce", factory.StaffSessionIdentityHandler.LastValidationAudience);
        Assert.Equal(1, factory.CommerceFinanceAreaHandler.RequestCount);
        Assert.Equal("staff-user-finance-token", factory.CommerceFinanceAreaHandler.LastAccessToken);
        Assert.Equal("/internal/v1/finance-area", factory.CommerceFinanceAreaHandler.LastPath);
    }

    [Fact(DisplayName = nameof(FinanceArea_StopsWhenTheIdentitySessionWasRevoked))]
    [Trait("Layer", "BffAdmin finance area - EndToEnd")]
    public async Task FinanceArea_StopsWhenTheIdentitySessionWasRevoked()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidateStatus = HttpStatusCode.Unauthorized;
        factory.StaffSessionIdentityHandler.ValidateCode = "SESSION_REQUIRED";

        using var response = await GetFinanceAreaAsync(client, login.Cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.CommerceFinanceAreaHandler.RequestCount);
        Assert.Equal(1, factory.StaffSessionIdentityHandler.ValidationCount);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.CommerceFinanceAreaHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "finance-area-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static Task<HttpResponseMessage> GetFinanceAreaAsync(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/finance-area");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static StaffSessionValidatedV1 ValidatedSession(
        Guid accountId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        string? accessToken = null)
        => new(
            accountId,
            "Marina Alves",
            roles,
            permissions,
            DateTimeOffset.UtcNow.AddMinutes(30),
            accessToken);

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
