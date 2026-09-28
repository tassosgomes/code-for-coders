using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class AuditRecordDetailTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(AuditRecordDetail_ResolvesDistinctReferencesAndReturnsTheAuditDetail))]
    public async Task AuditRecordDetail_ResolvesDistinctReferencesAndReturnsTheAuditDetail()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);
        var detail = await response.Content.ReadFromJsonAsync<AuditRecordDetailV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuditRecordSearchHandler.RecordId, factory.AuditRecordSearchHandler.LastRecordId);
        Assert.Equal(2, factory.AuditIdentityReferenceHandler.LastRequest?.References.Count);
        Assert.Equal("audit-audience-token", factory.AuditRecordSearchHandler.AccessToken);
        Assert.Equal("Marina Alves", detail?.Author?.Label);
        Assert.Equal("Rafael Silva", detail?.Target?.Label);
        Assert.Equal("Rafael Silva", detail?.Complements.Single().Author?.Label);
        Assert.Equal("identidade", detail?.Origin);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_RejectsRequestsWithoutASessionBeforeCallingAudit))]
    public async Task AuditRecordDetail_RejectsRequestsWithoutASessionBeforeCallingAudit()
    {
        ResetState();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/audit-records/{AuditRecordSearchHandler.RecordId:D}",
            TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_RejectsANonAdministratorBeforeCallingAudit))]
    public async Task AuditRecordDetail_RejectsANonAdministratorBeforeCallingAudit()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            Roles = ["professor"],
        };

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_ForwardsTheNeutralNotFoundResponse))]
    public async Task AuditRecordDetail_ForwardsTheNeutralNotFoundResponse()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.NotFound;
        factory.AuditRecordSearchHandler.ProblemCode = "AUDIT_RECORD_NOT_FOUND";

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "AUDIT_RECORD_NOT_FOUND");
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_DegradesWhenIdentityIsUnavailable))]
    public async Task AuditRecordDetail_DegradesWhenIdentityIsUnavailable()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.AuditIdentityReferenceHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        factory.AuditIdentityReferenceHandler.ProblemCode = "IDENTITY_UNAVAILABLE";

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);
        var detail = await response.Content.ReadFromJsonAsync<AuditRecordDetailV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuditRecordSearchHandler.AuthorId, detail?.Author?.Id);
        Assert.Null(detail?.Author?.Label);
        Assert.Null(detail?.Complements.Single().Author?.Label);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_ClosesTheResponseWhenIdentityRejectsAuthorization))]
    public async Task AuditRecordDetail_ClosesTheResponseWhenIdentityRejectsAuthorization()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.AuditIdentityReferenceHandler.StatusCode = HttpStatusCode.Forbidden;
        factory.AuditIdentityReferenceHandler.ProblemCode = "PERMISSION_DENIED";

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordDetail_ClosesARevokedSessionBeforeCallingAudit))]
    public async Task AuditRecordDetail_ClosesARevokedSessionBeforeCallingAudit()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidateStatus = HttpStatusCode.Unauthorized;
        factory.StaffSessionIdentityHandler.ValidateCode = "SESSION_REQUIRED";

        using var response = await GetDetailAsync(client, login, AuditRecordSearchHandler.RecordId);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.AuditRecordSearchHandler.Reset();
        factory.AuditIdentityReferenceHandler.Reset();
    }

    private async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", $"audit-detail-{Guid.CreateVersion7():N}");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        return new LoginResult(cookie, session);
    }

    private static Task<HttpResponseMessage> GetDetailAsync(
        HttpClient client,
        LoginResult login,
        Guid recordId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/audit-records/{recordId:D}");
        request.Headers.Add("Cookie", login.Cookie);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode, string code)
    {
        Assert.Equal(statusCode, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
