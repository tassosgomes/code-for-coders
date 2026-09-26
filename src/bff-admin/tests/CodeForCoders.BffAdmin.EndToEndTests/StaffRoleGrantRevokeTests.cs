using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffRoleGrantRevokeTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ListsMembersAndForwardsTheIdentitySession))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_ListsMembersAndForwardsTheIdentitySession()
    {
        ResetState();
        var accountId = Guid.CreateVersion7();
        factory.StaffMemberIdentityHandler.Page = new StaffMemberPageV1(
            [new StaffMemberV1(accountId, "Marina Alves", "marina@example.com", ["professor"], false)],
            new StaffMemberPaginationV1(2, 5, 6, 2));
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var session = await factory.SessionStore.GetAsync(login.Cookie.Split('=', 2)[1], TestContext.Current.CancellationToken);
        Assert.NotNull(session);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-members?page=2&size=5");
        request.Headers.Add("Cookie", login.Cookie);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var page = await response.Content.ReadFromJsonAsync<StaffMemberPageV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("staff-members:read", factory.StaffMemberIdentityHandler.LastAssertionScope);
        Assert.EndsWith("/internal/v1/staff-members?page=2&size=5", factory.StaffMemberIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(session!.IdentitySessionId, factory.StaffMemberIdentityHandler.LastStaffSessionId);
        Assert.Equal(accountId, Assert.Single(page!.Data).AccountId);
        Assert.Equal(2, page.Pagination.Page);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ForwardsTheGrantAndIdempotencyKey))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_ForwardsTheGrantAndIdempotencyKey()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var session = await factory.SessionStore.GetAsync(login.Cookie.Split('=', 2)[1], TestContext.Current.CancellationToken);
        var accountId = Guid.CreateVersion7();
        const string idempotencyKey = "bff-staff-role-grant-1";

        using var request = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{accountId:D}/role-grants",
            login,
            new StaffRoleActionRequestV1("suporte", "Cobertura temporária do atendimento."),
            idempotencyKey);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var action = await response.Content.ReadFromJsonAsync<StaffRoleActionResultV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("staff-members:write", factory.StaffMemberIdentityHandler.LastAssertionScope);
        Assert.EndsWith($"/internal/v1/staff-members/{accountId:D}/role-grants", factory.StaffMemberIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(session!.IdentitySessionId, factory.StaffMemberIdentityHandler.LastStaffSessionId);
        Assert.Equal(idempotencyKey, factory.StaffMemberIdentityHandler.LastIdempotencyKey);
        Assert.Equal(new StaffRoleActionRequestV1("suporte", "Cobertura temporária do atendimento."), factory.StaffMemberIdentityHandler.LastRequest);
        Assert.True(action!.Changed);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_ForwardsRevocationAndReportsEndedSessions))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_ForwardsRevocationAndReportsEndedSessions()
    {
        ResetState();
        factory.StaffMemberIdentityHandler.Action = new StaffRoleActionResultV1(
            new StaffMemberV1(Guid.CreateVersion7(), "Marina Alves", "marina@example.com", [], false),
            true,
            true);
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        var accountId = Guid.CreateVersion7();

        using var request = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{accountId:D}/role-revocations",
            login,
            new StaffRoleActionRequestV1("professor", "Fim da atuação como professora."),
            "bff-staff-role-revoke-1");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var action = await response.Content.ReadFromJsonAsync<StaffRoleActionResultV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.EndsWith($"/internal/v1/staff-members/{accountId:D}/role-revocations", factory.StaffMemberIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("staff-members:write", factory.StaffMemberIdentityHandler.LastAssertionScope);
        Assert.True(action!.SessionsEnded);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RevokedMemberSessionReceivesSessionRequiredOnTheNextBffAction))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_RevokedMemberSessionReceivesSessionRequiredOnTheNextBffAction()
    {
        ResetState();
        using var client = factory.CreateClient();
        var actor = await LoginAsync(client);
        var targetAccountId = Guid.CreateVersion7();
        var targetSessionId = Guid.CreateVersion7();
        factory.StaffSessionIdentityHandler.CreatedSession = new StaffSessionCreatedV1(
            targetSessionId,
            targetAccountId,
            "Rafaela Lima",
            ["professor"],
            ["autoria.ler"],
            DateTimeOffset.UtcNow.AddMinutes(60));
        var target = await LoginAsync(client);
        var storedTarget = await factory.SessionStore.GetAsync(
            target.Cookie.Split('=', 2)[1],
            TestContext.Current.CancellationToken);
        Assert.NotNull(storedTarget);
        factory.StaffMemberIdentityHandler.Action = new StaffRoleActionResultV1(
            new StaffMemberV1(targetAccountId, "Rafaela Lima", "rafaela@example.com", [], false),
            true,
            true);

        using var revoke = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{targetAccountId:D}/role-revocations",
            actor,
            new StaffRoleActionRequestV1("professor", "Papel removido."),
            "bff-staff-role-revoke-target-session");
        using var revokeResponse = await client.SendAsync(revoke, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        factory.StaffSessionIdentityHandler.RevokedSessionIds.Add(storedTarget!.IdentitySessionId);
        using var nextAction = new HttpRequestMessage(HttpMethod.Get, "/api/v1/staff-members");
        nextAction.Headers.Add("Cookie", target.Cookie);
        using var nextResponse = await client.SendAsync(nextAction, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await nextResponse.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, nextResponse.StatusCode);
        Assert.Equal("SESSION_REQUIRED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, factory.StaffMemberIdentityHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_MapsIdentityIdempotencyConflictToPublicCode))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_MapsIdentityIdempotencyConflictToPublicCode()
    {
        ResetState();
        factory.StaffMemberIdentityHandler.ActionStatus = HttpStatusCode.UnprocessableEntity;
        factory.StaffMemberIdentityHandler.ActionCode = "IDEMPOTENCY_CONFLICT";
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        using var request = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{Guid.CreateVersion7():D}/role-grants",
            login,
            new StaffRoleActionRequestV1("suporte", "Motivo informado."),
            "bff-staff-role-conflict");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_DeniesMissingPermissionBeforeCallingIdentity))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_DeniesMissingPermissionBeforeCallingIdentity()
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
        using var request = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{Guid.CreateVersion7():D}/role-grants",
            login,
            new StaffRoleActionRequestV1("suporte", "Motivo informado."),
            "bff-staff-role-denied");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, factory.StaffMemberIdentityHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(StaffRoleGrantRevoke_RejectsAReasonAboveTheContractLimitBeforeCallingIdentity))]
    [Trait("Layer", "BffAdmin staff role management - EndToEnd")]
    public async Task StaffRoleGrantRevoke_RejectsAReasonAboveTheContractLimitBeforeCallingIdentity()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        using var request = CreateRoleRequest(
            HttpMethod.Post,
            $"/api/v1/staff-members/{Guid.CreateVersion7():D}/role-grants",
            login,
            new StaffRoleActionRequestV1("suporte", new string('m', 1001)),
            "bff-staff-role-long-reason");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.StaffMemberIdentityHandler.RequestCount);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.StaffMemberIdentityHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", "staff-role-session-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static HttpRequestMessage CreateRoleRequest(
        HttpMethod method,
        string path,
        LoginResult login,
        StaffRoleActionRequestV1 body,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
