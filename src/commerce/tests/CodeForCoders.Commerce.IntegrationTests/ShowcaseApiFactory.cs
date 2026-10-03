using System.Security.Cryptography;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.IntegrationTests;

/// <summary>
/// Real <c>commerce</c> host with both authentication schemes and the real verifier. Assertions are signed by the
/// real <c>bff-student</c> factory (linked source), so the BFF and the verifier are checked against each other.
/// </summary>
public sealed class ShowcaseApiFactory(CommerceIntegrationFixture fixture, params Guid[] allowedTenants) : WebApplicationFactory<Program>
{
    public const string KeyId = "local-commerce-1";

    private readonly RSA bffKey = RSA.Create(2048);

    public IdentityJwksMessageHandler JwksHandler { get; } = new();

    public string CreateAssertion(Guid tenant, string scope = ServiceAssertionScopes.ShowcaseRead)
        => new ServiceAssertionTokenFactory(
            Options.Create(new StudentIdentityOptions { TenantId = tenant.ToString("D"), SigningKeyId = "identity-key", SigningKeyBase64 = Convert.ToBase64String(bffKey.ExportPkcs8PrivateKey()) }),
            Options.Create(new CommerceServiceOptions { SigningKeyId = KeyId, Scope = scope, SigningKeyBase64 = Convert.ToBase64String(bffKey.ExportPkcs8PrivateKey()) }),
            TimeProvider.System).Create(ServiceAssertionDestination.Commerce, scope);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        using var commerceKey = System.Security.Cryptography.RSA.Create(2048);
        builder.UseSetting("StudentAccountIdentity:SigningKeyBase64", Convert.ToBase64String(commerceKey.ExportPkcs8PrivateKey()));
        builder.UseEnvironment("ShowcaseTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", fixture.PostgreSql.GetConnectionString());
        builder.UseSetting("Valkey:ConnectionString", fixture.ValkeyConnectionString);
        builder.UseSetting("FinanceAreaTokens:Issuer", "identity");
        builder.UseSetting("FinanceAreaTokens:Audience", "commerce");
        builder.UseSetting("FinanceAreaTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.UseSetting("RabbitMq:Username", "commerce-test");
        builder.UseSetting("RabbitMq:Password", "commerce-test");
        builder.UseSetting("ServiceAssertions:Audience", "commerce");
        builder.UseSetting($"ServiceAssertions:Issuers:bff-student:PublicKeys:{KeyId}", Convert.ToBase64String(bffKey.ExportSubjectPublicKeyInfo()));
        builder.UseSetting("ServiceAssertions:Issuers:bff-student:AllowedScopes:0", ServiceAssertionScopes.ShowcaseRead);
        builder.UseSetting("ServiceAssertions:Issuers:bff-student:AllowedScopes:1", ServiceAssertionScopes.PurchaseIntentWrite);
        for (var index = 0; index < allowedTenants.Length; index++)
        {
            builder.UseSetting($"ServiceAssertions:Issuers:bff-student:AllowedTenantIds:{index}", allowedTenants[index].ToString("D"));
        }

        builder.ConfigureTestServices(services =>
        {
            foreach (var hostedService in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList())
            {
                services.Remove(hostedService);
            }

            services.AddHttpClient(FinanceAreaJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => JwksHandler);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            bffKey.Dispose();
            JwksHandler.Dispose();
        }

        base.Dispose(disposing);
    }
}
