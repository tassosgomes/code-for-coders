using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Billing.EndToEndTests;

public sealed class BillingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_billing")
        .WithUsername("code_for_coders_billing")
        .WithPassword("code_for_coders_billing")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new BillingDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", PostgreSql.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }
        });
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class BillingApiCollection : ICollectionFixture<BillingApiFactory>
{
    public const string Name = "billing-e2e";
}
