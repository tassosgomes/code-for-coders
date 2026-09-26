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
using Testcontainers.Redis;
using Xunit;

namespace CodeForCoders.Identity.EndToEndTests;

public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ServiceTenantId = "00000000-0000-7000-8000-000000000001";
    private const string StudentIssuer = "bff-student";
    private const string AdminIssuer = "bff-admin";
    private const string ServiceAssertionKeyId = "test-key";
    private const string AdminAssertionKeyId = "admin-test-key";
    private static readonly RSA ServiceAssertionKey = RSA.Create(2048);
    private static readonly RSA AdminAssertionKey = RSA.Create(2048);
    private static readonly string[] StudentScopes =
    [
        "student-accounts:create",
        "student-accounts:confirm",
        "student-accounts:request-confirmation",
        "student-sessions:create",
        "student-sessions:validate",
        "student-sessions:revoke",
        "student-password-resets:request",
        "student-password-resets:execute",
        "student-password-changes:execute",
    ];
    private static readonly string[] StaffScopes =
    [
        "staff-sessions:create",
        "staff-sessions:validate",
        "staff-sessions:revoke",
        "staff-passwords:reset",
        "staff-invitations:read",
        "staff-invitations:write",
        "staff-members:read",
        "staff-members:write",
    ];
    public PostgreSqlContainer PostgreSql { get; } = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("code_for_coders_identity")
        .WithUsername("code_for_coders_identity")
        .WithPassword("code_for_coders_identity")
        .Build();

    public RedisContainer Valkey { get; } = new RedisBuilder("redis:8.0").Build();

    public async ValueTask InitializeAsync()
    {
        await PostgreSql.StartAsync();
        await Valkey.StartAsync();

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
        builder.UseSetting("Valkey:ConnectionString", $"{Valkey.GetConnectionString()},abortConnect=false");
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("StudentAccount:ConfirmationBaseUrl", "https://students.example.test/confirm-account");
        builder.UseSetting("StudentAccount:ConfirmationLifetimeHours", "24");
        builder.UseSetting("Idempotency:FingerprintKeyBase64", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("OutboxProtection:KeyBase64", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("ServiceAssertions:Audience", "identity-internal");
        builder.UseSetting($"ServiceAssertions:Issuers:{StudentIssuer}:PublicKeys:{ServiceAssertionKeyId}", Convert.ToBase64String(ServiceAssertionKey.ExportSubjectPublicKeyInfo()));
        for (var index = 0; index < StudentScopes.Length; index++)
        {
            builder.UseSetting($"ServiceAssertions:Issuers:{StudentIssuer}:AllowedScopes:{index}", StudentScopes[index]);
        }

        builder.UseSetting($"ServiceAssertions:Issuers:{StudentIssuer}:AllowedTenantIds:0", ServiceTenantId);
        builder.UseSetting($"ServiceAssertions:Issuers:{AdminIssuer}:PublicKeys:{AdminAssertionKeyId}", Convert.ToBase64String(AdminAssertionKey.ExportSubjectPublicKeyInfo()));
        for (var index = 0; index < StaffScopes.Length; index++)
        {
            builder.UseSetting($"ServiceAssertions:Issuers:{AdminIssuer}:AllowedScopes:{index}", StaffScopes[index]);
        }

        builder.UseSetting($"ServiceAssertions:Issuers:{AdminIssuer}:AllowedTenantIds:0", ServiceTenantId);
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
        => CreateServiceAssertion(StudentIssuer, ServiceAssertionKey, ServiceAssertionKeyId, scope);

    public static string CreateAdminServiceAssertion(string scope)
        => CreateServiceAssertion(AdminIssuer, AdminAssertionKey, AdminAssertionKeyId, scope);

    public static string CreateServiceAssertion(string issuer, RSA signingKey, string keyId, string scope)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT",
            kid = keyId,
        }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer,
            aud = "identity-internal",
            sub = issuer,
            tenantId = ServiceTenantId,
            scope,
            jti = Guid.CreateVersion7(now).ToString("D"),
            iat = now.ToUnixTimeSeconds(),
            nbf = now.AddSeconds(-5).ToUnixTimeSeconds(),
            exp = now.AddSeconds(30).ToUnixTimeSeconds(),
        }));
        var signature = signingKey.SignData(
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
        await Valkey.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IdentityApiCollection : ICollectionFixture<IdentityApiFactory>
{
    public const string Name = "identity-e2e";
}
