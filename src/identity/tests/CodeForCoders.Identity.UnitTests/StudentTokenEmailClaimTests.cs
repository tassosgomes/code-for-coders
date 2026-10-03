using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class StudentTokenEmailClaimTests
{
    [Theory(DisplayName = nameof(EmailIsOnlyIssuedForConfiguredMediaAudience))]
    [InlineData("media", "student@example.com", true)]
    [InlineData("learning", "student@example.com", false)]
    [InlineData("other", "student@example.com", false)]
    [InlineData("media", null, false)]
    public void EmailIsOnlyIssuedForConfiguredMediaAudience(string audience, string? email, bool expected)
    {
        using var key = RSA.Create(2048);
        var issuer = new StudentSessionTokenIssuer(Options.Create(new StudentSessionTokenOptions
        {
            SigningKeyId = "student-key",
            SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            AudienceScopes = new() { ["media"] = "playback:use", ["learning"] = "lessons:read", ["other"] = "other:use" },
        }), TimeProvider.System);
        var token = issuer.Create(audience, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), email);
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        using var document = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
        Assert.Equal(expected, document.RootElement.TryGetProperty("email", out var claim));
        if (expected) Assert.Equal(email, claim.GetString());
        Assert.Equal(audience == "media" ? "playback:use" : audience == "learning" ? "lessons:read" : "other:use",
            document.RootElement.GetProperty("scope").GetString());
        Assert.False(document.RootElement.TryGetProperty("permissions", out _));
    }
}
