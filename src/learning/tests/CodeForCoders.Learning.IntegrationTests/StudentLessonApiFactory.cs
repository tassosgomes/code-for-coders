using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Learning.Api.Clients;
using CodeForCoders.Learning.Api.Security;
using CodeForCoders.Learning.Application.Common;
using CodeForCoders.Learning.Application.Interfaces;
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

public sealed class StudentLessonApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly RSA key = RSA.Create(2048);
    private readonly RSA assertionKey = RSA.Create(2048);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:18").Build();
    public LessonCommerceBoundaryHandler Commerce { get; } = new();
    public string DatabaseConnection => database.GetConnectionString();
    public RsaSecurityKey AssertionPublicKey => new(assertionKey);
    public async ValueTask InitializeAsync()
    {
        await database.StartAsync(TestContext.Current.CancellationToken);
        await using var db = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseNpgsql(DatabaseConnection).Options, new TenantContext());
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", DatabaseConnection);
        builder.UseSetting("RabbitMq:Username", "test"); builder.UseSetting("RabbitMq:Password", "test");
        builder.UseSetting("LearningTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.UseSetting("AccessDecision:BaseAddress", "http://commerce.test/");
        builder.UseSetting("AccessDecision:SigningKeyId", "learning-test");
        builder.UseSetting("AccessDecision:SigningKeyBase64", Convert.ToBase64String(assertionKey.ExportPkcs8PrivateKey()));
        builder.ConfigureTestServices(services =>
        {
            foreach (var item in services.Where(item => item.ServiceType == typeof(IHostedService)).ToList()) services.Remove(item);
            var p = key.ExportParameters(false);
            var jwks = JsonSerializer.Serialize(new { keys = new[] { new { kid = "student-test", kty = "RSA", use = "sig", alg = "RS256", n = Base64UrlEncoder.Encode(p.Modulus!), e = Base64UrlEncoder.Encode(p.Exponent!) } } });
            services.AddHttpClient(LearningJwksConfigurationManager.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new CourseJwksHandler(jwks));
            services.AddHttpClient<IStudentCourseAccessClient, StudentCourseAccessClient>().ConfigurePrimaryHttpMessageHandler(() => Commerce);
            services.AddHttpClient<IAccessDecisionClient, AccessDecisionClient>().ConfigurePrimaryHttpMessageHandler(() => Commerce);
        });
    }
    public HttpClient Student(Guid tenant, Guid student, string scope = "lessons:read", bool actor = false, string audience = "learning")
    {
        var claims = new List<Claim> { new("tenantId", tenant.ToString()), new("sub", student.ToString()), new("sessionId", Guid.CreateVersion7().ToString()), new("scope", scope) };
        if (actor) claims.Add(new("permissions", "autoria.ler"));
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("identity", audience, claims, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5), new SigningCredentials(new RsaSecurityKey(key) { KeyId = "student-test" }, SecurityAlgorithms.RsaSha256));
        var client = CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token)); return client;
    }
    public new async ValueTask DisposeAsync() { await base.DisposeAsync(); key.Dispose(); assertionKey.Dispose(); await database.DisposeAsync(); }
}

[CollectionDefinition(Name)]
public sealed class StudentLessonCollection : ICollectionFixture<StudentLessonApiFactory>
{
    public const string Name = "student-lessons";
}
