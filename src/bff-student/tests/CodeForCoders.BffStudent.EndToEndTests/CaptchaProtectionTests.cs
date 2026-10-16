using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Contracts;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodeForCoders.BffStudent.EndToEndTests;

/// <summary>ADR-0023: captcha and per-IP limit on the anonymous student endpoints. Cloudflare is replaced by <see cref="FakeTurnstileHandler"/>.</summary>
[Collection(BffStudentApiCollection.Name)]
public sealed class CaptchaProtectionTests(BffStudentApiFactory factory)
{
    private const string Login = "/api/v1/student-sessions";
    private const string Registration = "/api/v1/student-accounts";
    private const string PasswordReset = "/api/v1/password-reset-requests";

    public static TheoryData<string> AllEndpoints => new() { Login, Registration, PasswordReset };

    public static TheoryData<string> FailClosedEndpoints => new() { Registration, PasswordReset };

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

    [Theory]
    [MemberData(nameof(FailClosedEndpoints))]
    public async Task SiteverifyUnavailable_FailClosedEndpoints_Return503(string path)
    {
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(path, FakeTurnstileHandler.UnavailableToken), TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "CAPTCHA_UNAVAILABLE");
    }

    [Fact]
    public async Task SiteverifyUnavailable_LoginFailsOpenAndProceeds()
    {
        factory.StudentSessionClient.CreateResult = new StudentSessionCreatedResult(
            200, null, Guid.CreateVersion7(), Guid.CreateVersion7(), "Ana Souza", DateTimeOffset.FromUnixTimeSeconds(253402300000));
        using var client = ClientWithoutToken();

        using var response = await client.SendAsync(Request(Login, FakeTurnstileHandler.UnavailableToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_ProceedsAndSendsCloudflareIpToSiteverify()
    {
        factory.StudentRegistrationClient.Result = new StudentRegistrationResult(StatusCodes.Status202Accepted, null);
        using var client = ClientWithoutToken();
        using var request = Request(Registration, FakeTurnstileHandler.ValidToken);
        request.Headers.Add("CF-Connecting-IP", "203.0.113.77");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains(factory.Turnstile.Calls, call => call.RemoteIp == "203.0.113.77");
    }

    [Fact]
    public async Task ExceedingTheIpLimit_Returns429WithRetryAfter_PerIpAndPerEndpoint()
    {
        factory.StudentPasswordRecoveryClient.RequestResult = new(StatusCodes.Status202Accepted, null);
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
            Content = path switch
            {
                Login => JsonContent.Create(new { email = "ana@example.com", password = "SenhaForte1!" }),
                Registration => JsonContent.Create(new StudentRegistrationRequestV1("Ana Souza", "ana@example.com", "SenhaForte1!")),
                _ => JsonContent.Create(new StudentPasswordResetRequestV1("ana@example.com")),
            },
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
