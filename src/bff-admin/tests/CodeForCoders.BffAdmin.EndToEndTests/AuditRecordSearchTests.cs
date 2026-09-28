using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Api.ApiModels;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

[Collection(BffAdminApiCollection.Name)]
public sealed class AuditRecordSearchTests(BffAdminApiFactory factory)
{
    [Fact(DisplayName = nameof(AuditRecordSearch_AdministratorRevalidatesAndReceivesLabeledAuditPage))]
    public async Task AuditRecordSearch_AdministratorRevalidatesAndReceivesLabeledAuditPage()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        var from = new DateTimeOffset(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

        using var response = await SearchAsync(client, login,
            new AuditRecordSearchRequestV1(1, 20, null, from, null, "papel-concedido", null, null, true));
        var page = await response.Content.ReadFromJsonAsync<AuditRecordPageV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("professor", page?.Data.Single().Role);
        Assert.Equal("Marina Alves", page?.Data.Single().Author?.Label);
        Assert.Equal("Rafael Silva", page?.Data.Single().Target?.Label);
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal("audit-audience-token", factory.AuditRecordSearchHandler.AccessToken);
        Assert.Equal(from, factory.AuditRecordSearchHandler.LastRequest?.From);
        Assert.Equal("papel-concedido", factory.AuditRecordSearchHandler.LastRequest?.Type);
        Assert.Equal("audit", factory.StaffSessionIdentityHandler.LastValidationAudience);
        Assert.Equal(1, factory.AuditIdentityReferenceHandler.RequestCount);
        Assert.Equal("audit-references:read", factory.AuditIdentityReferenceHandler.AssertionScope);
        Assert.Equal(factory.StaffSessionIdentityHandler.CreatedSession.SessionId, factory.AuditIdentityReferenceHandler.StaffSessionId);
        Assert.Equal(2, factory.AuditIdentityReferenceHandler.LastRequest?.References.Count);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_OnlyIdentityReferencesAreSentForLabels))]
    public async Task AuditRecordSearch_OnlyIdentityReferencesAreSentForLabels()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        var studentId = Guid.CreateVersion7();
        factory.AuditRecordSearchHandler.Page = new AuditRecordPageV1(
            [
                new AuditRecordSummaryV1(
                    Guid.CreateVersion7(),
                    "matricula-registrada",
                    DateTimeOffset.UtcNow,
                    new AuditRecordIdentityReferenceV1("sistema", Guid.Empty),
                    new AuditRecordIdentityReferenceV1("aluno", studentId),
                    false,
                    false),
                new AuditRecordSummaryV1(
                    Guid.CreateVersion7(),
                    "papel-concedido",
                    DateTimeOffset.UtcNow,
                    new AuditRecordIdentityReferenceV1("conta-interna", AuditRecordSearchHandler.AuthorId),
                    new AuditRecordIdentityReferenceV1("conta-interna", Guid.Empty),
                    false,
                    false),
            ],
            new AuditRecordPaginationV1(1, 20, 2, 1, "snap_7mQ2kV4b123456789012345678901234567890123"));

        using var response = await SearchAsync(client, login);
        var page = await response.Content.ReadFromJsonAsync<AuditRecordPageV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reference = Assert.Single(factory.AuditIdentityReferenceHandler.LastRequest!.References);
        Assert.Equal(("conta-interna", AuditRecordSearchHandler.AuthorId), (reference.Type, reference.Id));
        Assert.Null(page?.Data[0].Author?.Label);
        Assert.Null(page?.Data[0].Target?.Label);
        Assert.Equal("Marina Alves", page?.Data[1].Author?.Label);
        Assert.Null(page?.Data[1].Target?.Label);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_NonAdministratorIsRejectedBeforeCallingAudit))]
    public async Task AuditRecordSearch_NonAdministratorIsRejectedBeforeCallingAudit()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            Roles = ["professor"],
        };

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_MissingCsrfIsRejectedBeforeIdentityAndAuditCalls))]
    public async Task AuditRecordSearch_MissingCsrfIsRejectedBeforeIdentityAndAuditCalls()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);

        using var response = await SearchAsync(client, login, includeCsrf: false);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "CSRF_INVALID");
        Assert.Equal(0, factory.StaffSessionIdentityHandler.ValidationCount);
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_RevokedSessionIsClosedBeforeAuditCall))]
    public async Task AuditRecordSearch_RevokedSessionIsClosedBeforeAuditCall()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidateStatus = HttpStatusCode.Unauthorized;
        factory.StaffSessionIdentityHandler.ValidateCode = "SESSION_REQUIRED";

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        Assert.Equal(0, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(1, factory.SessionStore.RemoveCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_ForwardsExpiredSnapshotResponse))]
    public async Task AuditRecordSearch_ForwardsExpiredSnapshotResponse()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.UnprocessableEntity;
        factory.AuditRecordSearchHandler.ProblemCode = "AUDIT_FILTER_INVALID";

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "AUDIT_FILTER_INVALID");
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_IdentityUnavailableReturnsThePageWithoutLabels))]
    public async Task AuditRecordSearch_IdentityUnavailableReturnsThePageWithoutLabels()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        factory.AuditIdentityReferenceHandler.StatusCode = HttpStatusCode.ServiceUnavailable;
        factory.AuditIdentityReferenceHandler.ProblemCode = "IDENTITY_UNAVAILABLE";

        using var response = await SearchAsync(client, login);
        var page = await response.Content.ReadFromJsonAsync<AuditRecordPageV1>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(page?.Data.Single().Author?.Label);
        Assert.Null(page?.Data.Single().Target?.Label);
        Assert.Equal(1, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_IdentityForbiddenClosesTheResponse))]
    public async Task AuditRecordSearch_IdentityForbiddenClosesTheResponse()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        factory.AuditIdentityReferenceHandler.StatusCode = HttpStatusCode.Forbidden;
        factory.AuditIdentityReferenceHandler.ProblemCode = "PERMISSION_DENIED";

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        Assert.Equal(1, factory.AuditRecordSearchHandler.RequestCount);
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_IdentitySessionRequiredClosesTheResponse))]
    public async Task AuditRecordSearch_IdentitySessionRequiredClosesTheResponse()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "audit-audience-token",
        };
        factory.AuditIdentityReferenceHandler.StatusCode = HttpStatusCode.Unauthorized;
        factory.AuditIdentityReferenceHandler.ProblemCode = "SESSION_REQUIRED";

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
    }

    [Fact(DisplayName = nameof(AuditRecordSearch_PropagatesAnInvalidAuditTokenResponse))]
    public async Task AuditRecordSearch_PropagatesAnInvalidAuditTokenResponse()
    {
        ResetState();
        using var client = factory.CreateClient();
        var login = await LoginAsync(client);
        factory.StaffSessionIdentityHandler.ValidatedSession = factory.StaffSessionIdentityHandler.ValidatedSession with
        {
            AccessToken = "invalid-audit-token",
        };
        factory.AuditRecordSearchHandler.StatusCode = HttpStatusCode.Unauthorized;
        factory.AuditRecordSearchHandler.ProblemCode = "TOKEN_INVALID";

        using var response = await SearchAsync(client, login);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "TOKEN_INVALID");
        Assert.Equal(0, factory.AuditIdentityReferenceHandler.RequestCount);
    }

    private void ResetState()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        factory.AuditRecordSearchHandler.Reset();
        factory.AuditIdentityReferenceHandler.Reset();
    }

    private static async Task<LoginResult> LoginAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/staff-sessions")
        {
            Content = JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!")),
        };
        request.Headers.Add("Idempotency-Key", $"audit-search-{Guid.CreateVersion7():N}");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<StaffSessionResponse>(TestContext.Current.CancellationToken);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';', 2)[0];
        Assert.NotNull(session);
        return new LoginResult(cookie, session);
    }

    private static async Task<HttpResponseMessage> SearchAsync(
        HttpClient client,
        LoginResult login,
        AuditRecordSearchRequestV1? search = null,
        bool includeCsrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audit-record-searches")
        {
            Content = JsonContent.Create(search ?? new AuditRecordSearchRequestV1(1, 20, null, null, null, null, null, null, null)),
        };
        request.Headers.Add("Cookie", login.Cookie);
        request.Headers.Add("Origin", "http://localhost:8081");
        if (includeCsrf)
        {
            request.Headers.Add("X-CSRF-Token", login.Session.CsrfToken);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode statusCode, string code)
    {
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private sealed record LoginResult(string Cookie, StaffSessionResponse Session);
}
