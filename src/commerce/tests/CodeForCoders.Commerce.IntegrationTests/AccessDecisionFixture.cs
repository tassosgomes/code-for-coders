using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class AccessDecisionFixture : IAsyncDisposable
{
    private const string Issuer = "access-decision-test";
    private const string KeyId = "access-decision-test-key";
    private readonly RSA key = RSA.Create(2048);
    private readonly MeterListener metrics = new();
    public CourtesyGrantFixture Courtesy { get; }
    public Guid OtherTenant { get; } = Guid.CreateVersion7();
    public ConcurrentQueue<string> Measurements { get; } = new();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public AccessDecisionFixture(CommerceIntegrationFixture infra)
    {
        var tenant = Guid.CreateVersion7();
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        Courtesy = new(infra, services => services.Configure<ServiceAssertionOptions>(options =>
        {
            // Only ephemeral test issuers are configured. No production consumer keys are provisioned.
            options.Issuers[Issuer] = new()
            {
                PublicKeys = new() { [KeyId] = publicKey },
                AllowedScopes = [ServiceAssertionScopes.AccessDecisionRead, ServiceAssertionScopes.ShowcaseRead],
                AllowedTenantIds = [tenant.ToString("D"), OtherTenant.ToString("D")]
            };
            options.Issuers["bff-student"] = new()
            {
                PublicKeys = new() { [KeyId] = publicKey },
                AllowedScopes = ServiceAssertionScopes.Student,
                AllowedTenantIds = [tenant.ToString("D")]
            };
        }), tenant);
        metrics.InstrumentPublished = (instrument, listener) => listener.EnableMeasurementEvents(instrument);
        metrics.SetMeasurementEventCallback<long>(CaptureMeasurement);
        metrics.SetMeasurementEventCallback<double>(CaptureMeasurement);
        metrics.Start();
    }

    private void CaptureMeasurement<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        => Measurements.Enqueue(instrument.Name + " " + string.Join(" ", tags.ToArray().Select(tag => $"{tag.Key}={tag.Value}")));

    public string Assertion(string scope = ServiceAssertionScopes.AccessDecisionRead, Guid? tenant = null,
        string issuer = Issuer, string audience = "commerce", int lifetimeSeconds = 30, string keyId = KeyId)
    {
        var now = Courtesy.Clock.GetUtcNow();
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT", kid = keyId }));
        var body = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer,
            sub = issuer,
            aud = audience,
            scope,
            tenantId = (tenant ?? Courtesy.Tenant).ToString("D"),
            jti = Guid.CreateVersion7().ToString("D"),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.AddSeconds(-1).ToUnixTimeSeconds(),
            exp = now.AddSeconds(lifetimeSeconds).ToUnixTimeSeconds()
        }));
        var signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{body}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{body}.{Encode(signature)}";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task<StudentAccessGrant> GrantAsync(int months = 6, string type = "months")
    {
        Courtesy.Authorize();
        object period = type == "months" ? new { type, months } : new { type };
        var body = new
        {
            studentId = Courtesy.Student,
            courseId = Courtesy.Course,
            accessPeriod = period,
            reason = "Test scholarship"
        };
        using var response = await Courtesy.GrantAsync(body, Guid.CreateVersion7().ToString("D"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentAccessGrant>(Cancellation))!;
    }

    public string Path(Guid? student = null, Guid? course = null)
        => $"/internal/v1/access-decision?studentId={student ?? Courtesy.Student:D}&courseId={course ?? Courtesy.Course:D}";

    public Task<HttpResponseMessage> RequestAsync(string? assertion, string? path = null)
    {
        Courtesy.Client.DefaultRequestHeaders.Authorization = assertion is null ? null : new("Bearer", assertion);
        return Courtesy.Client.GetAsync(path ?? Path(), Cancellation);
    }

    public async Task<DecideAccessOutput> DecideAsync(Guid? student = null, Guid? course = null, Guid? tenant = null)
    {
        using var response = await RequestAsync(Assertion(tenant: tenant), Path(student, course));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.CacheControl.MaxAge);
        var payload = await response.Content.ReadAsStringAsync(Cancellation);
        AccessDecisionContract.AssertValid(payload);
        var result = JsonSerializer.Deserialize<DecideAccessOutput>(payload, JsonSerializerOptions.Web)!;
        Assert.Equal(Courtesy.Clock.Now, result.DecidedAt);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        metrics.Dispose();
        await Courtesy.DisposeAsync();
        key.Dispose();
    }
}
