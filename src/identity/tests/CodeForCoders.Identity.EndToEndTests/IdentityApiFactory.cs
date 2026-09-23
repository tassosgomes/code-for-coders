using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Infra.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    public const string ServiceTenantId = "00000000-0000-7000-8000-000000000001";
    private const string ServiceAssertionKeyId = "test-key";
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
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("ServiceAssertions:Issuer", "bff-student");
        builder.UseSetting("ServiceAssertions:Audience", "identity-internal");
        builder.UseSetting($"ServiceAssertions:PublicKeys:{ServiceAssertionKeyId}", Convert.ToBase64String(ServiceAssertionKey.ExportSubjectPublicKeyInfo()));
        builder.UseSetting("ServiceAssertions:AllowedTenantIds:0", ServiceTenantId);
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

    public static string CreateServiceAssertion(string scope)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT",
            kid = ServiceAssertionKeyId,
        }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = "bff-student",
            aud = "identity-internal",
            sub = "bff-student",
            tenantId = ServiceTenantId,
            scope,
            jti = Guid.CreateVersion7(now).ToString("D"),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.AddSeconds(-5).ToUnixTimeSeconds(),
            exp = now.AddSeconds(30).ToUnixTimeSeconds(),
        }));
        var signature = ServiceAssertionKey.SignData(
            Encoding.ASCII.GetBytes($"{header}.{claims}"),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
