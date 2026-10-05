using System.Net;
using System.Security.Cryptography;
using CodeForCoders.Commerce.Api.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class AccessDecisionIssuersTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    [Fact(DisplayName = nameof(LearningAssertionCanReadDecision))]
    public async Task LearningAssertionCanReadDecision()
    {
        await using var test = new AccessDecisionFixture(hosts.Courtesy);
        ConfigureLearning(test);
        using var response = await test.RequestAsync(test.Assertion(issuer: "learning"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = nameof(LearningCannotUseStudentShowcaseScope))]
    public async Task LearningCannotUseStudentShowcaseScope()
    {
        await using var test = new AccessDecisionFixture(hosts.Courtesy);
        ConfigureLearning(test);
        using var response = await test.RequestAsync(test.Assertion(issuer: "learning", scope: "showcase:read"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = nameof(UnregisteredIssuerCannotReadDecision))]
    public async Task UnregisteredIssuerCannotReadDecision()
    {
        await using var test = new AccessDecisionFixture(hosts.Courtesy);
        using var response = await test.RequestAsync(test.Assertion(issuer: "learning"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = nameof(MediaAssertionCanReadDecision))]
    public async Task MediaAssertionCanReadDecision()
    {
        await using var test = new AccessDecisionFixture(hosts.Courtesy);
        ConfigureMedia(test);
        using var response = await test.RequestAsync(test.Assertion(issuer: "media"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = nameof(MediaCannotUseShowcaseScope))]
    public async Task MediaCannotUseShowcaseScope()
    {
        await using var test = new AccessDecisionFixture(hosts.Courtesy);
        ConfigureMedia(test);
        using var response = await test.RequestAsync(test.Assertion(issuer: "media", scope: "showcase:read"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void ConfigureMedia(AccessDecisionFixture test)
    {
        var settings = test.Courtesy.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceAssertionOptions>>().Value;
        var trusted = settings.Issuers["access-decision-test"];
        settings.Issuers["media"] = new() { PublicKeys = trusted.PublicKeys, AllowedTenantIds = trusted.AllowedTenantIds, AllowedScopes = ["access-decision:read"] };
    }

    private static void ConfigureLearning(AccessDecisionFixture test)
    {
        var settings = test.Courtesy.Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ServiceAssertionOptions>>().Value;
        var trusted = settings.Issuers["access-decision-test"];
        settings.Issuers["learning"] = new() { PublicKeys = trusted.PublicKeys, AllowedTenantIds = trusted.AllowedTenantIds, AllowedScopes = ["access-decision:read"] };
    }
}
