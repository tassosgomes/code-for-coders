using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffInvitationIssuingTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(StaffInvitationIssuing_ForwardsInvitationAndSessionToIdentity))]
    [Trait("Layer", "BffAdmin staff invitation - EndToEnd")]
    public async Task StaffInvitationIssuing_ForwardsInvitationAndSessionToIdentity()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var session = await factory.SessionStore.GetAsync(login.Cookie.Split('=', 2)[1], TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitations")
        {
            Content = JsonContent.Create(new CreateStaffInvitationRequestV1(
                "guest@example.com",
                "professor",
                "Vai atuar na trilha avançada.")),
        };
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", "bff-staff-invitation-1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/staff-invitations/" + factory.StaffInvitationIdentityHandler.Created.InvitationId.ToString("D"), response.Headers.Location?.OriginalString);
        Assert.Equal("guest@example.com", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("staff-invitations:write", factory.StaffInvitationIdentityHandler.LastAssertionScope);
        Assert.Equal(session!.IdentitySessionId, factory.StaffInvitationIdentityHandler.LastStaffSessionId);
        Assert.Equal("bff-staff-invitation-1", factory.StaffInvitationIdentityHandler.LastIdempotencyKey);
        Assert.Equal(new CreateStaffInvitationRequestV1(
            "guest@example.com",
            "professor",
            "Vai atuar na trilha avançada."), factory.StaffInvitationIdentityHandler.LastRequest);
    }

    [Fact(DisplayName = nameof(StaffInvitationIssuing_ListsPendingInvitationsThroughIdentity))]
    [Trait("Layer", "BffAdmin staff invitation - EndToEnd")]
    public async Task StaffInvitationIssuing_ListsPendingInvitationsThroughIdentity()
    {
        ResetState();
        var invitationId = Guid.CreateVersion7();
        factory.StaffInvitationIdentityHandler.Page = new StaffInvitationPageV1(
            [new PendingStaffInvitationV1(
                invitationId,
                "guest@example.com",
                "suporte",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(7))],
            new InvitationPaginationV1(2, 5, 6, 2));
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-invitations?page=2&size=5");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var page = await response.Content.ReadFromJsonAsync<StaffInvitationPageV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("staff-invitations:read", factory.StaffInvitationIdentityHandler.LastAssertionScope);
        Assert.EndsWith("/internal/v1/staff-invitations?page=2&size=5", factory.StaffInvitationIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(invitationId, Assert.Single(page!.Data).InvitationId);
        Assert.Equal(2, page.Pagination.Page);
    }

    [Fact(DisplayName = nameof(StaffInvitationIssuing_DeniesMissingPermissionBeforeCallingIdentity))]
    [Trait("Layer", "BffAdmin staff invitation - EndToEnd")]
    public async Task StaffInvitationIssuing_DeniesMissingPermissionBeforeCallingIdentity()
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

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitations")
        {
            Content = JsonContent.Create(new CreateStaffInvitationRequestV1(
                "guest@example.com",
                "professor",
                "Motivo do convite.")),
        };
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", "bff-staff-invitation-denied");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, factory.StaffInvitationIdentityHandler.RequestCount);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.StaffInvitationIdentityHandler.Reset();
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
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
