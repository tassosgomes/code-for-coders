using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CatalogCourseApiFactory(CommerceIntegrationFixture fixture) : WebApplicationFactory<Program>
{
    public string? DatabaseConnectionString { get; init; }

    public Action<IServiceCollection>? CustomizeServices { get; set; }

    public IdentityJwksMessageHandler JwksHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        using var commerceKey = System.Security.Cryptography.RSA.Create(2048);
        builder.UseSetting("StudentAccountIdentity:SigningKeyBase64", Convert.ToBase64String(commerceKey.ExportPkcs8PrivateKey()));
        builder.UseEnvironment("CatalogTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", DatabaseConnectionString ?? fixture.PostgreSql.GetConnectionString());
        builder.UseSetting("Valkey:ConnectionString", fixture.ValkeyConnectionString);
        builder.UseSetting("FinanceAreaTokens:Issuer", "identity");
        builder.UseSetting("FinanceAreaTokens:Audience", "commerce");
        builder.UseSetting("FinanceAreaTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.UseSetting("RabbitMq:Host", fixture.RabbitMq.Hostname);
        builder.UseSetting("RabbitMq:Port", fixture.RabbitMq.GetMappedPublicPort(5672).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(FinanceAreaJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => JwksHandler);
            CustomizeServices?.Invoke(services);
        });
    }
}
