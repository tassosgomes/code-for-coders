using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Identity.EndToEndTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly RSA ServiceAssertionKey = RSA.Create(2048);
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_identity")
        .WithUsername("code_for_coders_identity")
        .WithPassword("code_for_coders_identity")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new IdentityDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", PostgreSql.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StudentAccount:ConfirmationBaseUrl", "https://students.example.test/confirm-account");
        builder.UseSetting("StudentAccount:ConfirmationLifetimeHours", "24");
        builder.UseSetting("Idempotency:FingerprintKeyBase64", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("ServiceAssertions:Issuer", "bff-student");
        builder.UseSetting("ServiceAssertions:Audience", "identity-internal");
        builder.UseSetting("ServiceAssertions:PublicKeys:test-key", Convert.ToBase64String(ServiceAssertionKey.ExportSubjectPublicKeyInfo()));
        builder.UseSetting("ServiceAssertions:AllowedTenantIds:0", "00000000-0000-7000-8000-000000000001");
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
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "identity-e2e";
}
