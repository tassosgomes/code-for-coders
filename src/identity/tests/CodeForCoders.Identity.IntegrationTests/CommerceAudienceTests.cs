using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using Microsoft.Extensions.Options;
using Xunit;
namespace CodeForCoders.Identity.IntegrationTests;

public sealed class CommerceAudienceTests
{
    [Fact(DisplayName = nameof(CommerceStudentTokenHasOrdersScopeAndSignedBuyer))]
    public void CommerceStudentTokenHasOrdersScopeAndSignedBuyer()
    {
        var tenant = Guid.CreateVersion7(); var student = Guid.CreateVersion7(); var session = Guid.CreateVersion7();
        using var jwt = Payload(CreateIssuer().Create("commerce", tenant, student, session));
        Assert.Equal("identity", jwt.RootElement.GetProperty("iss").GetString()); Assert.Equal("commerce", jwt.RootElement.GetProperty("aud").GetString());
        Assert.Equal("orders:use", jwt.RootElement.GetProperty("scope").GetString()); Assert.Equal(student.ToString(), jwt.RootElement.GetProperty("sub").GetString());
        Assert.Equal(tenant.ToString(), jwt.RootElement.GetProperty("tenantId").GetString()); Assert.Equal(session.ToString(), jwt.RootElement.GetProperty("sessionId").GetString());
    }
    [Fact(DisplayName = nameof(CommerceStudentTokenNeverContainsEmailOrStaffPermissions))]
    public void CommerceStudentTokenNeverContainsEmailOrStaffPermissions()
    {
        using var jwt = Payload(CreateIssuer().Create("commerce", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "private@example.test"));
        Assert.False(jwt.RootElement.TryGetProperty("email", out _)); Assert.False(jwt.RootElement.TryGetProperty("permissions", out _)); Assert.False(jwt.RootElement.TryGetProperty("roles", out _));
    }
    private static StudentSessionTokenIssuer CreateIssuer()
    {
        using var key = RSA.Create(2048);
        return new(Options.Create(new StudentSessionTokenOptions { Issuer = "identity", SigningKeyId = "student-test", SigningKeyBase64 = Convert.ToBase64String(key.ExportPkcs8PrivateKey()), AudienceScopes = new() { ["commerce"] = "orders:use", ["media"] = "playback:use" } }), TimeProvider.System);
    }
    private static JsonDocument Payload(string token)
    {
        var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4)));
    }
}
