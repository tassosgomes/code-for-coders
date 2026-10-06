using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

using CodeForCoders.Billing.Infra.Gateway;

namespace CodeForCoders.Billing.IntegrationTests;

public sealed class BillingTestApp : WebApplicationFactory<Program>
{
    private readonly BillingIntegrationFixture fixture;
    private readonly RSA commerceRsa = RSA.Create(2048);
    public const string CommerceKeyId = "local-commerce-billing-1";
    public const string WebhookSecret = "whsec_test_stripe_secret_12345";
    public const string StripeSecretKey = "sk_test_local";
    public Guid AllowedTenantId { get; } = Guid.CreateVersion7();
    public string ProcessingNamespace { get; } = $"it-billing-{Guid.CreateVersion7():N}";
    public Mock<IPaymentGateway> GatewayMock { get; } = new();

    public BillingTestApp(BillingIntegrationFixture fixture)
    {
        this.fixture = fixture;
        GatewayMock.Setup(g => g.VerifyEvent(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string body, string? sig) => StripeEventTranslator.Verify(body, sig, WebhookSecret, TimeProvider.System.GetUtcNow()));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", fixture.PostgreSql.GetConnectionString());
        builder.UseSetting("Billing:Namespace", ProcessingNamespace);
        builder.UseSetting("RabbitMq:Host", fixture.RabbitMq.Hostname);
        builder.UseSetting("RabbitMq:Port", fixture.RabbitMq.GetMappedPublicPort(5672).ToString());
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("RabbitMq:Exchange", $"billing.{ProcessingNamespace}.events");
        builder.UseSetting("RabbitMq:DeadLetterExchange", $"billing.{ProcessingNamespace}.dlx");
        builder.UseSetting("RabbitMq:HeartbeatQueue", $"billing.{ProcessingNamespace}.heartbeat");
        builder.UseSetting("Valkey:ConnectionString", fixture.ValkeyConnectionString);

        builder.UseSetting("Stripe:SecretKey", StripeSecretKey);
        builder.UseSetting("Stripe:WebhookSigningSecret", WebhookSecret);
        builder.UseSetting("PaymentReturn:AllowedHosts:0", "localhost");
        builder.UseSetting("PaymentReturn:AllowedHosts:1", "app.code4coders.com.br");

        builder.UseSetting("ServiceAssertions:Audience", "billing");
        builder.UseSetting($"ServiceAssertions:Issuers:commerce:PublicKeys:{CommerceKeyId}", Convert.ToBase64String(commerceRsa.ExportSubjectPublicKeyInfo()));
        builder.UseSetting("ServiceAssertions:Issuers:commerce:AllowedScopes:0", "payment:request");
        builder.UseSetting("ServiceAssertions:Issuers:commerce:AllowedTenantIds:0", AllowedTenantId.ToString("D"));
        builder.UseSetting("ServiceAssertions:Issuers:commerce:AllowedTenantIds:1", "00000000-0000-7000-8000-000000000001");

        builder.ConfigureTestServices(services =>
        {
            foreach (var worker in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Namespace?.StartsWith("CodeForCoders.Billing", StringComparison.Ordinal) == true).ToList())
            {
                services.Remove(worker);
            }
            services.AddSingleton(GatewayMock.Object);
        });
    }

    public string CreateCommerceAssertion(Guid tenantId, string scope = "payment:request", string issuer = "commerce", string audience = "billing", TimeSpan? lifetime = null, RSA? overrideKey = null)
    {
        var key = overrideKey ?? commerceRsa;
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = CommerceKeyId }));
        var now = DateTimeOffset.UtcNow;
        var exp = now + (lifetime ?? TimeSpan.FromSeconds(45));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = issuer,
            aud = audience,
            sub = issuer,
            tenantId = tenantId.ToString("D"),
            jti = Guid.CreateVersion7().ToString("D"),
            scope,
            iat = now.ToUnixTimeSeconds(),
            nbf = now.ToUnixTimeSeconds(),
            exp = exp.ToUnixTimeSeconds()
        }));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");
        var signature = Base64Url(key.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        return $"{header}.{claims}.{signature}";
    }

    public static string CreateStripeSignature(string body, string secret, DateTimeOffset? now = null)
    {
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public AsyncServiceScope CreateScope(Guid? tenantId = null)
    {
        var scope = Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.SetNamespace(ProcessingNamespace);
        tenantContext.Set(tenantId ?? Guid.CreateVersion7());
        return scope;
    }

    public async Task<BillingDbContext> CreateDbContextAsync(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(fixture.PostgreSql.GetConnectionString())
            .Options;
        var tenant = new TenantContext();
        tenant.SetNamespace(ProcessingNamespace);
        tenant.Set(tenantId ?? Guid.CreateVersion7());
        var db = new BillingDbContext(options, tenant);
        return db;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            commerceRsa.Dispose();
        }
        base.Dispose(disposing);
    }
}
