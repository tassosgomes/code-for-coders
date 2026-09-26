using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class StaffRoleChangeTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(StaffRoleChange_ForwardsBothRolesAndReturnsEndedSessions))]
    [Trait("Layer", "BffAdmin staff role change - EndToEnd")]
    public async Task StaffRoleChange_ForwardsBothRolesAndReturnsEndedSessions()
    {
        ResetState();
        factory.StaffMemberIdentityHandler.Action = new StaffRoleActionResultV1(
            new StaffMemberV1(Guid.CreateVersion7(), "Marina Alves", "marina@example.com", ["financeiro"], false),
            true,
            true);
        using var client = factory.CreateClient();
        var actor = await LoginAsync(client);
        var session = await factory.SessionStore.GetAsync(
            actor.Cookie.Split('=', 2)[1],
            TestContext.Current.CancellationToken);
        Assert.NotNull(session);
        var accountId = Guid.CreateVersion7();
        const string idempotencyKey = "bff-staff-role-change-1";
        var requestBody = new StaffRoleChangeRequestV1(
            "professor",
            "financeiro",
            "Mudou de função para o time financeiro.");

        using var request = CreateRoleChangeRequest(accountId, actor, requestBody, idempotencyKey);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var action = await response.Content.ReadFromJsonAsync<StaffRoleActionResultV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("staff-members:write", factory.StaffMemberIdentityHandler.LastAssertionScope);
        Assert.EndsWith($"/internal/v1/staff-members/{accountId:D}/role-changes", factory.StaffMemberIdentityHandler.LastRequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(session!.IdentitySessionId, factory.StaffMemberIdentityHandler.LastStaffSessionId);
        Assert.Equal(idempotencyKey, factory.StaffMemberIdentityHandler.LastIdempotencyKey);
        Assert.Equal(requestBody, factory.StaffMemberIdentityHandler.LastChangeRequest);
        Assert.True(action!.Changed);
        Assert.True(action.SessionsEnded);
    }

    [Fact(DisplayName = nameof(StaffRoleChange_MapsMissingSourceRoleToThePublicProblem))]
    [Trait("Layer", "BffAdmin staff role change - EndToEnd")]
    public async Task StaffRoleChange_MapsMissingSourceRoleToThePublicProblem()
    {
        ResetState();
        factory.StaffMemberIdentityHandler.ActionStatus = HttpStatusCode.UnprocessableEntity;
        factory.StaffMemberIdentityHandler.ActionCode = "ROLE_NOT_HELD";
        using var client = factory.CreateClient();
        var actor = await LoginAsync(client);
        using var request = CreateRoleChangeRequest(
            Guid.CreateVersion7(),
            actor,
            new StaffRoleChangeRequestV1("professor", "financeiro", "Mudou de função."),
            "bff-staff-role-change-source-missing");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var problem = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ROLE_NOT_HELD", problem.RootElement.GetProperty("code").GetString());
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
        request.Headers.Add("Idempotency-Key", "staff-role-change-session-login");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static HttpRequestMessage CreateRoleChangeRequest(
        Guid accountId,
        LoginResult actor,
        StaffRoleChangeRequestV1 body,
        string idempotencyKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/staff-members/{accountId:D}/role-changes")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Cookie", actor.Cookie);
        request.Headers.Add("X-CSRF-Token", actor.Session.CsrfToken);
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
