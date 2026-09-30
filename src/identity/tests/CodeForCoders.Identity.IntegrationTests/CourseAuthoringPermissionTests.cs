using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Identity.Api.Security;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeForCoders.Identity.IntegrationTests;

public sealed class CourseAuthoringPermissionTests
{
    [Fact(DisplayName = nameof(CourseAuthoringPermission_IssuesTeacherTokenWithLearningScopeAndPermission))]
    [Trait("Layer", "Identity media audience - Integration")]
    public void CourseAuthoringPermission_IssuesTeacherTokenWithLearningScopeAndPermission()
    {
        var issuer = CreateIssuer();
        var token = issuer.Create(
            "learning",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [StaffRoleCatalog.Teacher],
            StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Teacher));
        using var jwt = ReadTokenPayload(token);

        Assert.Equal("identity", jwt.RootElement.GetProperty("iss").GetString());
        Assert.Equal("learning", jwt.RootElement.GetProperty("aud").GetString());
        Assert.Equal("courses:authoring", jwt.RootElement.GetProperty("scope").GetString());
        var permissions = jwt.RootElement.GetProperty("permissions").EnumerateArray()
            .Select(permission => permission.GetString())
            .ToArray();
        Assert.Contains("autoria.editar", permissions);
        Assert.Contains("autoria.ler", permissions);
    }

    [Fact(DisplayName = nameof(CourseAuthoringPermission_GrantsSendPermissionOnlyToTeacherRole))]
    [Trait("Layer", "Identity media audience - Integration")]
    public void CourseAuthoringPermission_GrantsSendPermissionOnlyToTeacherRole()
    {
        Assert.Contains("autoria.editar", StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Teacher));
        Assert.DoesNotContain("autoria.editar", StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Administrator));
        Assert.DoesNotContain("autoria.editar", StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Finance));
        Assert.DoesNotContain("autoria.editar", StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Support));
    }

    [Fact(DisplayName = nameof(CourseAuthoringPermission_RejectsAudienceThatIsNotConfigured))]
    [Trait("Layer", "Identity media audience - Integration")]
    public void CourseAuthoringPermission_RejectsAudienceThatIsNotConfigured()
    {
        var issuer = CreateIssuer();

        Assert.True(issuer.CanIssueFor("learning"));
        Assert.False(issuer.CanIssueFor("commerce-admin"));
        Assert.Throws<InvalidOperationException>(() => issuer.Create(
            "commerce-admin",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            [StaffRoleCatalog.Teacher],
            StaffRoleCatalog.GetPermissions(StaffRoleCatalog.Teacher)));
    }

    private static StaffSessionTokenIssuer CreateIssuer()
    {
        using var rsa = RSA.Create(2048);
        var settings = new StaffSessionTokenOptions
        {
            Issuer = "identity",
            SigningKeyId = "media-test",
            SigningKeyBase64 = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()),
            AudienceScopes = new Dictionary<string, string> { ["learning"] = "courses:authoring" },
        };
        return new StaffSessionTokenIssuer(Options.Create(settings), TimeProvider.System);
    }

    private static JsonDocument ReadTokenPayload(string token)
    {
        var encodedPayload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var paddedPayload = encodedPayload + new string('=', (4 - encodedPayload.Length % 4) % 4);
        return JsonDocument.Parse(Convert.FromBase64String(paddedPayload));
    }
}
