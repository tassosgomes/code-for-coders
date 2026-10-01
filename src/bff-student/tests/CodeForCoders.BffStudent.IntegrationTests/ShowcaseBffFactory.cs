using System.Security.Cryptography;
using CodeForCoders.BffStudent.Api.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;

namespace CodeForCoders.BffStudent.IntegrationTests;

/// <summary>Real BFF host: real pipeline, assertion factory, resilience and typed client; only the far side of the wire is controlled.</summary>
public sealed class ShowcaseBffFactory(BffStudentIntegrationFixture fixture) : WebApplicationFactory<Program>
{
    public const string TenantId = "00000000-0000-7000-8000-000000000001";
    public const string CommerceKeyId = "test-commerce-key";
    public const string IdentityKeyId = "test-identity-key";

    private readonly RSA commerceKey = RSA.Create(2048);
    private readonly RSA identityKey = RSA.Create(2048);

    public CommerceBoundaryHandler Commerce { get; } = new();

    public CountingSessionIdentityClient Identity { get; } = new();

    public byte[] CommercePublicKey => commerceKey.ExportSubjectPublicKeyInfo();

    public byte[] IdentityPublicKey => identityKey.ExportSubjectPublicKeyInfo();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("ShowcaseTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", fixture.PostgreSql.GetConnectionString());
        builder.UseSetting("Valkey:ConnectionString", fixture.ValkeyConnectionString);
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StudentIdentity:BaseAddress", "http://identity.integration.test/");
        builder.UseSetting("StudentIdentity:SigningKeyId", IdentityKeyId);
        builder.UseSetting("StudentIdentity:SigningKeyBase64", Convert.ToBase64String(identityKey.ExportPkcs8PrivateKey()));
        builder.UseSetting("StudentIdentity:TenantId", TenantId);
        builder.UseSetting("Commerce:BaseAddress", "http://commerce.integration.test/");
        builder.UseSetting("Commerce:SigningKeyId", CommerceKeyId);
        builder.UseSetting("Commerce:SigningKeyBase64", Convert.ToBase64String(commerceKey.ExportPkcs8PrivateKey()));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStudentSessionIdentityClient>();
            services.AddSingleton<IStudentSessionIdentityClient>(Identity);
            services.AddHttpClient<IShowcaseCommerceClient, ShowcaseCommerceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => Commerce);
            // Same pipeline and limits as production; only the retry back-off is shortened to keep the suite fast.
            services.ConfigureAll<HttpStandardResilienceOptions>(options => options.Retry.Delay = TimeSpan.Zero);
            foreach (var hostedService in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToList())
            {
                services.Remove(hostedService);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            commerceKey.Dispose();
            identityKey.Dispose();
        }

        base.Dispose(disposing);
    }
}
