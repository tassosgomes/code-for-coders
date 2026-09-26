using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class FinanceAreaApiFactory : WebApplicationFactory<Program>
{
    public IdentityJwksMessageHandler JwksHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("FinanceAreaTest");
        builder.UseSetting("FinanceAreaTokens:Issuer", "identity");
        builder.UseSetting("FinanceAreaTokens:Audience", "commerce");
        builder.UseSetting("FinanceAreaTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.UseSetting("RabbitMq:Username", "commerce-test");
        builder.UseSetting("RabbitMq:Password", "commerce-test");
        builder.ConfigureTestServices(services =>
        {
            foreach (var hostedService in services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList())
            {
                services.Remove(hostedService);
            }

            services.AddHttpClient(FinanceAreaJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => JwksHandler);
        });
    }
}
