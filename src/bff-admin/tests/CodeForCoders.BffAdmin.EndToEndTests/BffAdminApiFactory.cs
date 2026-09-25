using CodeForCoders.BffAdmin.Application.Common;
using CodeForCoders.BffAdmin.Api.Clients;
using CodeForCoders.BffAdmin.Contracts;
using CodeForCoders.BffAdmin.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.BffAdmin.EndToEndTests;

public sealed class BffAdminApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public StaffPasswordResetIdentityHandler IdentityHandler { get; } = new();

    public string IdentityPublicKeyBase64 { get; private set; } = string.Empty;

    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_bff_admin")
        .WithUsername("code_for_coders_bff_admin")
        .WithPassword("code_for_coders_bff_admin")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();

        var dbOptions = new DbContextOptionsBuilder<BffAdminDbContext>()
            .UseNpgsql(PostgreSql.GetConnectionString())
            .Options;
        await using var dbContext = new BffAdminDbContext(dbOptions, new TenantContext());
        await dbContext.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("EndToEndTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", PostgreSql.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StaffIdentity:BaseAddress", "http://identity.test/");
        builder.UseSetting("StaffIdentity:Issuer", "bff-admin");
        builder.UseSetting("StaffIdentity:Audience", "identity-internal");
        builder.UseSetting("StaffIdentity:SigningKeyId", "e2e-test");
        builder.UseSetting("StaffIdentity:TenantId", "00000000-0000-7000-8000-000000000001");
        using var rsa = RSA.Create(2048);
        var privateKey = rsa.ExportPkcs8PrivateKey();
        IdentityPublicKeyBase64 = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
        builder.UseSetting("StaffIdentity:SigningKeyBase64", Convert.ToBase64String(privateKey));
        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }

            services.AddSingleton(IdentityHandler);
            services.AddHttpClient<IStaffPasswordResetIdentityClient, StaffPasswordResetIdentityClient>()
                .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
                    serviceProvider.GetRequiredService<StaffPasswordResetIdentityHandler>());
        });
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await PostgreSql.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class BffAdminApiCollection : ICollectionFixture<BffAdminApiFactory>
{
    public const string Name = "bff-admin-e2e";
}
