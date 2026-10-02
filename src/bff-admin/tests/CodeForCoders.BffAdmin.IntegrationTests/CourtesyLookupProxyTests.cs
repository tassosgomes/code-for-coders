using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.IntegrationTests;

public sealed class CourtesyLookupProxyTests
{
    private const string Email = "private.student@lookup.test";
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
    private static CourtesyBffApiFactory CreateFactory()
    {
        var factory = new CourtesyBffApiFactory();
        factory.Identity.Roles = ["financeiro"];
        factory.Identity.Permissions = ["financeiro.ler", "oferta.editar", "cortesia.conceder"];
        return factory;
    }

    [Fact(DisplayName = nameof(RealClientSendsSignedAssertionAndSessionAndKeepsPersonalDataOutOfTelemetry))]
    public async Task RealClientSendsSignedAssertionAndSessionAndKeepsPersonalDataOutOfTelemetry()
    {
        await using var factory = CreateFactory();
        using var client = await factory.AuthenticatedAsync();
        var spans = new ConcurrentBag<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is BffAdminTelemetry.ActivitySourceName or "Microsoft.AspNetCore" or "System.Net.Http",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => spans.Add(activity.DisplayName + string.Join(" ", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"))),
        };
        ActivitySource.AddActivityListener(listener);
        using var response = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        var account = await response.Content.ReadFromJsonAsync<StudentAccountV1>(CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(Email, account?.Email); Assert.Equal("Lookup Student", account?.Name); Assert.False(account?.EmailConfirmed);
        Assert.Equal("http://identity.test/internal/v1/student-account-lookups", factory.Lookup.Url);
        Assert.Equal(Email, factory.Lookup.Input?.Email);
        var session = await factory.Sessions.GetAsync("opaque-course-session", CancellationToken);
        Assert.Equal(session!.IdentitySessionId.ToString(), factory.Lookup.Session);
        var parts = factory.Lookup.Assertion!.Split('.');
        using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(factory.PublicKey), out _);
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var claims = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("student-account:lookup", claims.RootElement.GetProperty("scope").GetString());
        Assert.Equal("bff-admin", claims.RootElement.GetProperty("iss").GetString());
        Assert.Equal("identity-internal", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal("00000000-0000-7000-8000-000000000001", claims.RootElement.GetProperty("tenantId").GetString());
        var capturedLogs = factory.Logs.ToArray();
        var capturedSpans = spans.ToArray();
        Assert.NotEmpty(capturedLogs); Assert.NotEmpty(capturedSpans);
        Assert.All(capturedLogs.Concat(capturedSpans), entry => { Assert.DoesNotContain(Email, entry); Assert.DoesNotContain("Lookup Student", entry); });
    }

    [Fact(DisplayName = nameof(RequiresCookieAndCsrfBeforeCallingIdentityLookup))]
    public async Task RequiresCookieAndCsrfBeforeCallingIdentityLookup()
    {
        await using var factory = CreateFactory();
        using var anonymous = factory.CreateClient();
        using var noSession = await anonymous.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(noSession, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        using var client = await factory.AuthenticatedAsync(); client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        using var noCsrf = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(noCsrf, HttpStatusCode.Forbidden, "CSRF_INVALID");
        Assert.Equal(0, factory.Lookup.Calls);
    }

    [Fact(DisplayName = nameof(OtherRolesAndStudentHaveNoCourtesyPermission))]
    public async Task OtherRolesAndStudentHaveNoCourtesyPermission()
    {
        await using var factory = CreateFactory(); using var client = await factory.AuthenticatedAsync();
        foreach (var role in new[] { "professor", "suporte", "administrador", "aluno" })
        {
            factory.Identity.Roles = [role]; factory.Identity.Permissions = role == "administrador" ? ["acesso.gerir"] : [];
            using var response = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        }
        Assert.Equal(0, factory.Lookup.Calls);
    }

    [Fact(DisplayName = nameof(RevokedFinanceRoleAndSessionAreDeniedOnTheNextAction))]
    public async Task RevokedFinanceRoleAndSessionAreDeniedOnTheNextAction()
    {
        await using var factory = CreateFactory(); using var client = await factory.AuthenticatedAsync();
        using var first = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        factory.Identity.Permissions = []; factory.Identity.Roles = [];
        using var revokedRole = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(revokedRole, HttpStatusCode.Forbidden, "PERMISSION_DENIED");
        factory.Identity.Revoked = true;
        using var revokedSession = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(revokedSession, HttpStatusCode.Unauthorized, "SESSION_REQUIRED");
        Assert.Equal(1, factory.Lookup.Calls);
    }

    [Fact(DisplayName = nameof(PreservesKnownIdentityErrorsAndMapsUnavailableAndTimeout))]
    public async Task PreservesKnownIdentityErrorsAndMapsUnavailableAndTimeout()
    {
        await using var factory = CreateFactory(); using var client = await factory.AuthenticatedAsync();
        foreach (var item in new[] { (400, "VALIDATION_ERROR"), (401, "SESSION_REQUIRED"), (401, "SERVICE_UNAUTHORIZED"), (403, "PERMISSION_DENIED"), (404, "STUDENT_ACCOUNT_NOT_FOUND"), (500, "UNEXPECTED") })
        {
            factory.Lookup.Status = (HttpStatusCode)item.Item1; factory.Lookup.Code = item.Item2;
            using var response = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
            await AssertProblemAsync(response, item.Item1 == 500 ? HttpStatusCode.BadGateway : (HttpStatusCode)item.Item1, item.Item1 == 500 ? "IDENTITY_UNAVAILABLE" : item.Item2);
        }
        factory.Lookup.Unavailable = true;
        using var unavailable = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(unavailable, HttpStatusCode.BadGateway, "IDENTITY_UNAVAILABLE");
        factory.Lookup.Unavailable = false; factory.Lookup.Timeout = true;
        using var timeout = await client.PostAsJsonAsync("/api/v1/student-account-lookups", new { email = Email }, CancellationToken);
        await AssertProblemAsync(timeout, HttpStatusCode.GatewayTimeout, "UPSTREAM_TIMEOUT");
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(CancellationToken), cancellationToken: CancellationToken);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(Email, body.RootElement.GetRawText());
    }
}
