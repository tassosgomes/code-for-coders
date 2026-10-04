using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.UnitTests;

public sealed class StudentTokenAudienceTests
{
    private static JsonDocument Issue()
    {
        using var key = RSA.Create(2048);
        var issuer = new StudentSessionTokenIssuer(Options.Create(new StudentSessionTokenOptions
        {
            SigningKeyId = "student-key",
            SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()),
            AudienceScopes = new() { ["learning"] = "lessons:read" },
        }), TimeProvider.System);
        var token = issuer.Create("learning", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
    }

    [Fact(DisplayName = nameof(LearningAudienceHasOnlyLessonScope))]
    public void LearningAudienceHasOnlyLessonScope()
    {
        using var token = Issue();
        Assert.Equal("learning", token.RootElement.GetProperty("aud").GetString());
        Assert.Equal("lessons:read", token.RootElement.GetProperty("scope").GetString());
    }

    [Fact(DisplayName = nameof(StudentHasNoActorPermissions))]
    public void StudentHasNoActorPermissions()
    {
        using var token = Issue();
        Assert.False(token.RootElement.TryGetProperty("permissions", out _));
        Assert.False(token.RootElement.TryGetProperty("roles", out _));
    }

    [Fact(DisplayName = nameof(LearningTokenHasNoEmail))]
    public void LearningTokenHasNoEmail()
    {
        using var token = Issue(); Assert.False(token.RootElement.TryGetProperty("email", out _));
    }

    [Fact(DisplayName = nameof(StudentClaimsIdentifyTenantStudentSessionAndFreshToken))]
    public void StudentClaimsIdentifyTenantStudentSessionAndFreshToken()
    {
        using var token = Issue();
        foreach (var claim in new[] { "sub", "tenantId", "sessionId", "jti" })
            Assert.NotEqual(Guid.Empty, token.RootElement.GetProperty(claim).GetGuid());
        Assert.Equal("identity", token.RootElement.GetProperty("iss").GetString());
        Assert.Equal(300, token.RootElement.GetProperty("exp").GetInt64() - token.RootElement.GetProperty("iat").GetInt64());
    }

    [Fact(DisplayName = nameof(UnknownAudienceCannotBeIssued))]
    public void UnknownAudienceCannotBeIssued()
    {
        var issuer = new StudentSessionTokenIssuer(Options.Create(new StudentSessionTokenOptions()), TimeProvider.System);
        Assert.False(issuer.CanIssueFor("learning"));
        Assert.Throws<InvalidOperationException>(() => issuer.Create("learning", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()));
    }
}
