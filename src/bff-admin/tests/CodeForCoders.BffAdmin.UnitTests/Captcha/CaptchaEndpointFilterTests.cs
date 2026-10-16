using System.Net;
using CodeForCoders.BffAdmin.Api.Captcha;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.BffAdmin.UnitTests.Captcha;

public sealed class CaptchaEndpointFilterTests
{
    private static readonly object Next = new();

    [Fact]
    public async Task Disabled_PassesWithoutCallingVerifier()
    {
        var verifier = new FakeVerifier(TurnstileVerification.Failed([]));

        var (result, _) = await RunAsync(CaptchaFailurePolicy.FailClosed, verifier, enabled: false, token: null);

        Assert.Same(Next, result);
        Assert.Empty(verifier.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingToken_Returns400CaptchaRequiredWithoutCallingVerifier(string? token)
    {
        var verifier = new FakeVerifier(TurnstileVerification.Passed);

        var (result, http) = await RunAsync(CaptchaFailurePolicy.FailOpen, verifier, token: token);

        AssertProblem(result, http, 400, "CAPTCHA_REQUIRED");
        Assert.Empty(verifier.Calls);
    }

    [Fact]
    public async Task FailedToken_Returns403CaptchaInvalid()
    {
        var (result, http) = await RunAsync(CaptchaFailurePolicy.FailOpen, new FakeVerifier(TurnstileVerification.Failed(["invalid-input-response"])), token: "bad");

        AssertProblem(result, http, 403, "CAPTCHA_INVALID");
    }

    [Fact]
    public async Task OversizedToken_Returns403WithoutCallingVerifier()
    {
        var verifier = new FakeVerifier(TurnstileVerification.Passed);

        var (result, http) = await RunAsync(CaptchaFailurePolicy.FailOpen, verifier, token: new string('a', 2049));

        AssertProblem(result, http, 403, "CAPTCHA_INVALID");
        Assert.Empty(verifier.Calls);
    }

    [Fact]
    public async Task PassedToken_ContinuesAndSendsCloudflareIp()
    {
        var verifier = new FakeVerifier(TurnstileVerification.Passed);

        var (result, _) = await RunAsync(CaptchaFailurePolicy.FailClosed, verifier, token: "good", cfIp: "198.51.100.7");

        Assert.Same(Next, result);
        var call = Assert.Single(verifier.Calls);
        Assert.Equal(("good", "198.51.100.7"), call);
    }

    [Fact]
    public async Task Unavailable_FailOpen_Continues()
    {
        var (result, _) = await RunAsync(CaptchaFailurePolicy.FailOpen, new FakeVerifier(TurnstileVerification.Unavailable), token: "tok");

        Assert.Same(Next, result);
    }

    [Fact]
    public async Task Unavailable_FailClosed_Returns503()
    {
        var (result, http) = await RunAsync(CaptchaFailurePolicy.FailClosed, new FakeVerifier(TurnstileVerification.Unavailable), token: "tok");

        AssertProblem(result, http, 503, "CAPTCHA_UNAVAILABLE");
    }

    [Theory]
    [InlineData("198.51.100.7", "198.51.100.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("not-an-ip", "10.0.0.5")]
    [InlineData(null, "10.0.0.5")]
    public void ClientIpResolver_PrefersParseableCloudflareHeader_ElseRemoteAddress(string? header, string expected)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        if (header is not null)
        {
            http.Request.Headers["CF-Connecting-IP"] = header;
        }

        Assert.Equal(expected, ClientIpResolver.Resolve(http));
    }

    [Fact]
    public void ClientIpResolver_MapsIPv4MappedAddress()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.0.2.1");

        Assert.Equal("192.0.2.1", ClientIpResolver.Resolve(http));
    }

    private static async Task<(object? Result, DefaultHttpContext Http)> RunAsync(
        CaptchaFailurePolicy policy,
        FakeVerifier verifier,
        bool enabled = true,
        string? token = null,
        string? cfIp = null)
    {
        var services = new ServiceCollection()
            .AddSingleton<IOptions<TurnstileOptions>>(Options.Create(new TurnstileOptions { Enabled = enabled, SecretKey = "s" }))
            .AddSingleton<ITurnstileVerifier>(verifier)
            .AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance)
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (token is not null)
        {
            http.Request.Headers[CaptchaEndpointFilter.HeaderName] = token;
        }

        if (cfIp is not null)
        {
            http.Request.Headers["CF-Connecting-IP"] = cfIp;
        }

        var filter = new CaptchaEndpointFilter(policy);
        var result = await filter.InvokeAsync(EndpointFilterInvocationContext.Create(http), _ => ValueTask.FromResult<object?>(Next));
        return (result, http);
    }

    private static void AssertProblem(object? result, DefaultHttpContext http, int status, string code)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(status, problem.StatusCode);
        Assert.Equal(code, problem.ProblemDetails.Extensions["code"]);
        Assert.True(problem.ProblemDetails.Extensions.ContainsKey("traceId"));
        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
    }

    private sealed class FakeVerifier(TurnstileVerification result) : ITurnstileVerifier
    {
        public List<(string Token, string? Ip)> Calls { get; } = [];

        public Task<TurnstileVerification> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken)
        {
            Calls.Add((token, remoteIp));
            return Task.FromResult(result);
        }
    }
}
