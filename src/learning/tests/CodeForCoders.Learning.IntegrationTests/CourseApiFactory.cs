using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeForCoders.Learning.IntegrationTests;

public sealed class CourseApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseConnection => database.GetConnectionString();
    public bool FailPublicationOutbox { get; set; }

    private readonly RSA key = RSA.Create(2048);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18").Build();

    public async ValueTask InitializeAsync()
    {
        await database.StartAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new LearningDbContext(options, new TenantContext());
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", database.GetConnectionString());
        builder.UseSetting("RabbitMq:Username", "test");
        builder.UseSetting("RabbitMq:Password", "test");
        builder.UseSetting("LearningTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.ConfigureTestServices(services =>
        {
            foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService)).ToList())
                services.Remove(registration);
            services.AddScoped<CodeForCoders.Learning.Application.Interfaces.IOutboxMessageWriter>(provider =>
                new PublicationOutboxFailureWriter(new CodeForCoders.Learning.Infra.Data.Outbox.OutboxMessageWriter(provider.GetRequiredService<LearningDbContext>()), () => FailPublicationOutbox));
            var parameters = key.ExportParameters(false);
            var document = JsonSerializer.Serialize(new
            {
                keys = new[] { new { kid = "course-test", kty = "RSA", use = "sig", alg = "RS256",
                n = Base64UrlEncoder.Encode(parameters.Modulus!), e = Base64UrlEncoder.Encode(parameters.Exponent!) } }
            });
            services.AddHttpClient(LearningJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new CourseJwksHandler(document));
        });
    }

    public HttpClient Actor(Guid tenantId, Guid actorId, string[] permissions, string audience = "learning", bool expired = false)
    {
        var claims = new List<Claim> { new("tenantId", tenantId.ToString()), new("sub", actorId.ToString()) };
        claims.AddRange(permissions.Select(value => new Claim("permissions", value)));
        var now = DateTime.UtcNow;
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("identity", audience, claims,
            now.AddMinutes(-10), expired ? now.AddMinutes(-1) : now.AddMinutes(5),
            new SigningCredentials(new RsaSecurityKey(key) { KeyId = "course-test" }, SecurityAlgorithms.RsaSha256));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token));
        client.DefaultRequestHeaders.Add("X-Actor-Name", "Teacher display name");
        return client;
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        key.Dispose();
        await database.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class CourseApiCollection : ICollectionFixture<CourseApiFactory>
{
    public const string Name = "course-access";
}
