using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CatalogCourseApiFactory(CommerceIntegrationFixture fixture) : WebApplicationFactory<Program>
{
    public Action<IServiceCollection>? CustomizeServices { get; set; }

    public IdentityJwksMessageHandler JwksHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("CatalogTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", fixture.PostgreSql.GetConnectionString());
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
