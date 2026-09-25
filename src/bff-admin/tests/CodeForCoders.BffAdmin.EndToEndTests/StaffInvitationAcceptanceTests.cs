using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffInvitationAcceptanceTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(StaffInvitationAcceptance_LookupSendsTheTokenInTheRequestBodyWithoutAStaffSession))]
    [Trait("Layer", "BffAdmin staff invitation acceptance - EndToEnd")]
    public async Task StaffInvitationAcceptance_LookupSendsTheTokenInTheRequestBodyWithoutAStaffSession()
    {
        ResetState();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitation-lookups")
        {
            Content = JsonContent.Create(new InvitationTokenRequestV1("invitation-secret")),
        };
        request.Headers.Add("Origin", "http://localhost:8081");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var preview = await response.Content.ReadFromJsonAsync<StaffInvitationPreviewV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("professor", preview!.OfferedRole);
        Assert.Equal("staff-invitations:read", factory.StaffInvitationIdentityHandler.LastAssertionScope);
        Assert.Null(factory.StaffInvitationIdentityHandler.LastStaffSessionId);
        Assert.Equal(new InvitationTokenRequestV1("invitation-secret"), factory.StaffInvitationIdentityHandler.LastLookupRequest);
        Assert.EndsWith("/internal/v1/staff-invitation-lookups", factory.StaffInvitationIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain("invitation-secret", factory.StaffInvitationIdentityHandler.LastRequestUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_CreatesTheBffSessionAndReturnsTheOfferedRole))]
    [Trait("Layer", "BffAdmin staff invitation acceptance - EndToEnd")]
    public async Task StaffInvitationAcceptance_CreatesTheBffSessionAndReturnsTheOfferedRole()
    {
        ResetState();
        var identitySessionId = Guid.CreateVersion7();
        var accountId = Guid.CreateVersion7();
        factory.StaffInvitationIdentityHandler.Session = new StaffSessionCreatedV1(
            identitySessionId,
            accountId,
            "Marina Alves",
            ["professor"],
            ["autoria.ler"],
            DateTimeOffset.UtcNow.AddMinutes(55));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitation-acceptances")
        {
            Content = JsonContent.Create(new AcceptStaffInvitationRequestV1(
                "invitation-secret",
                "Marina Alves",
                "Tr1lha!Segura")),
        };
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", "bff-staff-invitation-accept-1");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var sessionResponse = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        var bffSession = await factory.SessionStore.GetAsync(
            cookie.Split('=', 2)[1],
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accountId, sessionResponse!.AccountId);
        Assert.Equal(["professor"], sessionResponse.Roles);
        Assert.Equal(["autoria.ler"], sessionResponse.Permissions);
        Assert.False(string.IsNullOrWhiteSpace(sessionResponse.CsrfToken));
        Assert.Contains("staff_session=", cookie, StringComparison.Ordinal);
        Assert.NotNull(bffSession);
        Assert.Equal(identitySessionId, bffSession!.IdentitySessionId);
        Assert.Equal("staff-invitations:write", factory.StaffInvitationIdentityHandler.LastAssertionScope);
        Assert.Null(factory.StaffInvitationIdentityHandler.LastStaffSessionId);
        Assert.Equal("bff-staff-invitation-accept-1", factory.StaffInvitationIdentityHandler.LastIdempotencyKey);
        Assert.Equal(
            new AcceptStaffInvitationRequestV1("invitation-secret", "Marina Alves", "Tr1lha!Segura"),
            factory.StaffInvitationIdentityHandler.LastAcceptanceRequest);
        Assert.DoesNotContain("invitation-secret", factory.StaffInvitationIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_ReturnsTheGenericExpiredLinkProblemWithoutCreatingASession))]
    [Trait("Layer", "BffAdmin staff invitation acceptance - EndToEnd")]
    public async Task StaffInvitationAcceptance_ReturnsTheGenericExpiredLinkProblemWithoutCreatingASession()
    {
        ResetState();
        factory.StaffInvitationIdentityHandler.AcceptStatus = HttpStatusCode.UnprocessableEntity;
        factory.StaffInvitationIdentityHandler.AcceptCode = "INVITATION_INVALID";
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitation-acceptances")
        {
            Content = JsonContent.Create(new AcceptStaffInvitationRequestV1(
                "expired-secret",
                "Marina Alves",
                "Tr1lha!Segura")),
        };
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", "bff-staff-invitation-expired");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("INVITATION_INVALID", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("Este convite não vale mais. Peça um novo ao administrador.", problem.RootElement.GetProperty("title").GetString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(1, factory.StaffInvitationIdentityHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(StaffInvitationAcceptance_RequiresAnAllowedOriginBeforeCreatingASession))]
    [Trait("Layer", "BffAdmin staff invitation acceptance - EndToEnd")]
    public async Task StaffInvitationAcceptance_RequiresAnAllowedOriginBeforeCreatingASession()
    {
        ResetState();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-invitation-acceptances")
        {
            Content = JsonContent.Create(new AcceptStaffInvitationRequestV1(
                "invitation-secret",
                "Marina Alves",
                "Tr1lha!Segura")),
        };
        request.Headers.Add("Idempotency-Key", "bff-staff-invitation-origin");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CSRF_INVALID", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, factory.StaffInvitationIdentityHandler.RequestCount);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.StaffInvitationIdentityHandler.Reset();
    }
}
