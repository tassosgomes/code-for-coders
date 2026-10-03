using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Identity.Api.Endpoints;
using CodeForCoders.Identity.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class StudentTokenJwksTests
{
    [Theory(DisplayName = nameof(JwksPublishesBothKeysAndRotationWithoutPrivateMaterial))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JwksPublishesBothKeysAndRotationWithoutPrivateMaterial(bool rotated)
    {
        using var student = RSA.Create(2048); using var staff = RSA.Create(2048); using var previous = RSA.Create(2048);
        var settings = Settings(student);
        if (rotated) settings.PreviousSigningPublicKeys["student-previous"] = Convert.ToBase64String(previous.ExportSubjectPublicKeyInfo());
        await using var app = await Server(staff, settings);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/internal/v1/jwks", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var keys = body.RootElement.GetProperty("keys").EnumerateArray().ToArray();
        Assert.Equal(rotated ? 3 : 2, keys.Length);
        Assert.Contains(keys, key => key.GetProperty("kid").GetString() == "staff");
        Assert.Contains(keys, key => key.GetProperty("kid").GetString() == "student");
        if (rotated) Assert.Contains(keys, key => key.GetProperty("kid").GetString() == "student-previous");
        foreach (var key in keys) Assert.Equal(new[] { "alg", "e", "kid", "kty", "n", "use" }, key.EnumerateObject().Select(p => p.Name).Order().ToArray());
    }

    [Fact(DisplayName = nameof(JwksKeyValidatesIssuedStudentSignature))]
    public async Task JwksKeyValidatesIssuedStudentSignature()
    {
        using var student = RSA.Create(2048); using var staff = RSA.Create(2048);
        var settings = Settings(student);
        await using var app = await Server(staff, settings);
        using var client = app.GetTestClient();
        using var body = JsonDocument.Parse(await client.GetStringAsync("/internal/v1/jwks", TestContext.Current.CancellationToken));
        var key = body.RootElement.GetProperty("keys").EnumerateArray().Single(k => k.GetProperty("kid").GetString() == "student");
        using var publicKey = RSA.Create();
        publicKey.ImportParameters(new RSAParameters { Modulus = Decode(key.GetProperty("n").GetString()!), Exponent = Decode(key.GetProperty("e").GetString()!) });
        var issuer = new StudentSessionTokenIssuer(Options.Create(settings), TimeProvider.System);
        var pieces = issuer.Create("learning", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Split('.');
        Assert.True(publicKey.VerifyData(Encoding.ASCII.GetBytes($"{pieces[0]}.{pieces[1]}"), Decode(pieces[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    private static StudentSessionTokenOptions Settings(RSA key) => new()
    {
        SigningKeyId = "student",
        SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
        AudienceScopes = new() { ["learning"] = "lessons:read" },
    };
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '='));
    private static async Task<WebApplication> Server(RSA staff, StudentSessionTokenOptions student)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IOptions<StudentSessionTokenOptions>>(Options.Create(student));
        builder.Services.AddSingleton<IOptions<StaffSessionTokenOptions>>(Options.Create(new StaffSessionTokenOptions
        { SigningKeyId = "staff", SigningKeyBase64 = Convert.ToBase64String(staff.ExportPkcs8PrivateKey()) }));
        builder.Services.AddSingleton<UserTokenSigningKeySet>();
        var app = builder.Build(); app.MapSigningKeyEndpoints(); await app.StartAsync(TestContext.Current.CancellationToken); return app;
    }
}
