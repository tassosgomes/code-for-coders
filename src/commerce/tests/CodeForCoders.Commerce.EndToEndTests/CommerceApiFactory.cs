using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Commerce.EndToEndTests;

public sealed class CommerceApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_commerce")
        .WithUsername("code_for_coders_commerce")
        .WithPassword("code_for_coders_commerce")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new CommerceDbContext(dbOptions, new TenantContext());
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
public sealed class CommerceApiCollection : ICollectionFixture<CommerceApiFactory>
{
    public const string Name = "commerce-e2e";
}
