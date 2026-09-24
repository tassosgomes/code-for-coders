using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Xunit;

namespace CodeForCoders.BffStudent.UnitTests.Clients;

internal sealed record CapturedIdentityRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? Authorization,
    string? IdempotencyKey,
    string Body);

internal sealed class FakeIdentityHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<CapturedIdentityRequest> Requests { get; } = [];

    public static FakeIdentityHandler Returning(HttpStatusCode statusCode, object? body = null)
        => new(() => new HttpResponseMessage(statusCode)
        {
            Content = body is null ? new StringContent(string.Empty) : JsonContent.Create(body),
        });

    public static FakeIdentityHandler ReturningRaw(HttpStatusCode statusCode, string body)
        => new(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    public static FakeIdentityHandler Throwing(Exception exception)
        => new(() => throw exception);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new CapturedIdentityRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null,
            request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
        return respond();
    }
}

internal static class IdentityClientTestSupport
{
    public const string TenantId = "00000000-0000-7000-8000-000000000001";
    public const string SigningKeyId = "bff-student-test";

    private static readonly RSA SigningKey = RSA.Create(2048);

    public static HttpClient CreateHttpClient(FakeIdentityHandler handler)
        => new(handler) { BaseAddress = new Uri("http://identity.test/") };

    public static ServiceAssertionTokenFactory CreateAssertionFactory()
        => new(
            Options.Create(new StudentIdentityOptions
            {
                BaseAddress = "http://identity.test/",
                Scope = string.Join(' ', AllScopes),
                SigningKeyId = SigningKeyId,
                SigningKeyBase64 = Convert.ToBase64String(SigningKey.ExportPkcs8PrivateKey()),
                TenantId = TenantId,
            }),
            TimeProvider.System);

    public static readonly string[] AllScopes =
    [
        "student-accounts:create",
        "student-accounts:confirm",
        "student-accounts:request-confirmation",
        "student-sessions:create",
        "student-sessions:validate",
        "student-sessions:revoke",
        "student-password-resets:request",
        "student-password-resets:execute",
        "student-password-changes:execute",
    ];

    public static TheoryData<string, int> TransportFailures => new()
    {
        { "connection-refused", 502 },
        { "resilience-timeout", 504 },
        { "circuit-open", 502 },
        { "attempt-cancelled", 504 },
    };

    public static Exception CreateTransportFailure(string failure) => failure switch
    {
        "connection-refused" => new HttpRequestException("connection refused"),
        "resilience-timeout" => new TimeoutRejectedException(),
        "circuit-open" => new BrokenCircuitException(),
        "attempt-cancelled" => new TaskCanceledException("attempt timed out"),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
    };

    public static void AssertServiceAssertion(CapturedIdentityRequest request, string expectedScope)
    {
        Assert.NotNull(request.Authorization);
        Assert.StartsWith("Bearer ", request.Authorization, StringComparison.Ordinal);
        var parts = request.Authorization["Bearer ".Length..].Split('.');
        Assert.Equal(3, parts.Length);

        Assert.True(
            SigningKey.VerifyData(
                Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                Base64UrlDecode(parts[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1),
            "Service assertion signature must verify with the BFF public key.");

        using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
        Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(SigningKeyId, header.RootElement.GetProperty("kid").GetString());

        using var claims = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        var root = claims.RootElement;
        Assert.Equal(expectedScope, root.GetProperty("scope").GetString());
        Assert.Equal("bff-student", root.GetProperty("iss").GetString());
        Assert.Equal("bff-student", root.GetProperty("sub").GetString());
        Assert.Equal("identity-internal", root.GetProperty("aud").GetString());
        Assert.Equal(Guid.Parse(TenantId), root.GetProperty("tenantId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("jti").GetString()));
        Assert.True(root.GetProperty("exp").GetInt64() > root.GetProperty("iat").GetInt64());
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
