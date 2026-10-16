using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffAdmin.Contracts;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

/// <summary>ADR-0023: captcha and per-IP limit on the anonymous staff endpoints. Cloudflare is replaced by <see cref="FakeTurnstileHandler"/>.</summary>
[Collection(BffAdminApiCollection.Name)]
public sealed class CaptchaProtectionTests(BffAdminApiFactory factory)
{
    private const string Login = "/api/v1/staff-sessions";
    private const string PasswordReset = "/api/v1/staff-password-reset-requests";

    public static TheoryData<string> AllEndpoints => new() { Login, PasswordReset };

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task MissingToken_Returns400CaptchaRequired(string path)
    {
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(path, null), TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "CAPTCHA_REQUIRED");
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task RejectedToken_Returns403CaptchaInvalid(string path)
    {
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(path, FakeTurnstileHandler.InvalidToken), TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "CAPTCHA_INVALID");
    }

    [Fact]
    public async Task SiteverifyUnavailable_PasswordResetRequestFailsClosedWith503()
    {
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(PasswordReset, FakeTurnstileHandler.UnavailableToken), TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CAPTCHA_UNAVAILABLE");
    }

    [Fact]
    public async Task SiteverifyUnavailable_LoginFailsOpenAndProceeds()
    {
        factory.SessionStore.Reset();
        factory.StaffSessionIdentityHandler.Reset();
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(Login, FakeTurnstileHandler.UnavailableToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_ProceedsAndSendsCloudflareIpToSiteverify()
    {
        factory.IdentityHandler.Reset();
        factory.IdentityHandler.ResponseStatus = HttpStatusCode.Accepted;
        using var client = ClientWithoutToken();
        using var request = Request(PasswordReset, FakeTurnstileHandler.ValidToken);
        request.Headers.Add("CF-Connecting-IP", "203.0.113.77");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(factory.Turnstile.Calls, call => call.RemoteIp == "203.0.113.77");
    }

    [Fact]
    public async Task ExceedingTheIpLimit_Returns429WithRetryAfter_PerIp()
    {
        factory.IdentityHandler.Reset();
        factory.IdentityHandler.ResponseStatus = HttpStatusCode.Accepted;
        using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("AnonymousRateLimit:PermitLimit", "2"));
        using var client = limited.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var allowed = await client.SendAsync(Request(PasswordReset, FakeTurnstileHandler.ValidToken, "198.51.100.1"), cancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var blocked = await client.SendAsync(Request(PasswordReset, FakeTurnstileHandler.ValidToken, "198.51.100.1"), cancellationToken);
        using var otherIp = await client.SendAsync(Request(PasswordReset, FakeTurnstileHandler.ValidToken, "198.51.100.2"), cancellationToken);

        await AssertProblemAsync(blocked, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        Assert.True(int.Parse(blocked.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture) >= 1);
        Assert.Equal(HttpStatusCode.Accepted, otherIp.StatusCode);
    }

    private HttpClient ClientWithoutToken()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Captcha-Token");
        return client;
    }

    private static HttpRequestMessage Request(string path, string? token, string? clientIp = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = path == Login
                ? JsonContent.Create(new StaffSessionLoginV1("operator@example.com", "SenhaForte1!"))
                : JsonContent.Create(new StaffPasswordRecoveryRequestV1("marina@example.com")),
        };
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        if (token is not null)
        {
            request.Headers.Add("X-Captcha-Token", token);
        }

        if (clientIp is not null)
        {
            request.Headers.Add("CF-Connecting-IP", clientIp);
        }

        return request;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? string.Empty);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
    }
}
