using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Infra.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class VideoLibraryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SigningKeyId = "media-integration";
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly MediaIntegrationFixture infrastructure = new();

    public string PlaybackMasterKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public System.Collections.Concurrent.ConcurrentDictionary<Guid, int> PlaybackDecisions { get; } = new();
    public System.Collections.Concurrent.ConcurrentDictionary<Guid, int> PlaybackDecisionCalls { get; } = new();

    public string JwksDocument { get; private set; } = string.Empty;

    public string MinioEndpoint => infrastructure.MinioEndpoint;

    public string VideoPreparationConnectionString => infrastructure.VideoPreparationConnectionString;

    public Uri RabbitMqEndpoint => new(infrastructure.RabbitMq.GetConnectionString());

    public async ValueTask InitializeAsync()
    {
        await infrastructure.InitializeAsync();
        JwksDocument = CreateJwksDocument(signingKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("ConnectionStrings:DefaultConnection", infrastructure.PostgreSql.GetConnectionString());
        builder.UseSetting("Media:Role", "api");
        builder.UseSetting("Preparation:MasterKey", PlaybackMasterKey);
        builder.UseSetting("Preparation:MasterKeyId", "playback-test");
        builder.UseSetting("Playback:Delivery:SharedSecret", "development-test-secret");
        builder.UseSetting("AccessDecision:SigningKeyId", "media-decision-test");
        builder.UseSetting("AccessDecision:SigningKeyBase64", Convert.ToBase64String(signingKey.ExportPkcs8PrivateKey()));
        builder.UseSetting("AccessDecision:BaseAddress", "http://commerce.test/");
        builder.UseSetting("AwsMedia:EndpointInternal", infrastructure.MinioEndpoint);
        builder.UseSetting("AwsMedia:EndpointPublic", infrastructure.MinioEndpoint);
        builder.UseSetting("AwsMedia:BucketName", MediaIntegrationFixture.MinioBucketName);
        builder.UseSetting("AwsMedia:AccessKeyId", MediaIntegrationFixture.MinioAccessKey);
        builder.UseSetting("AwsMedia:SecretAccessKey", MediaIntegrationFixture.MinioSecretKey);
        builder.UseSetting("AwsMedia:ForcePathStyle", "true");
        builder.UseSetting("RabbitMq:Username", "code_for_coders");
        builder.UseSetting("RabbitMq:Password", "code_for_coders");
        builder.UseSetting("MediaTokens:Issuer", "identity");
        builder.UseSetting("MediaTokens:Audience", "media");
        builder.UseSetting("MediaTokens:JwksUrl", "http://identity.test/internal/v1/jwks");
        builder.ConfigureTestServices(services =>
        {
            var hostedServices = services
                .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var hostedService in hostedServices)
            {
                services.Remove(hostedService);
            }

            services.AddHttpClient<CodeForCoders.Media.Application.Interfaces.IAccessDecisionClient, CodeForCoders.Media.Api.Clients.AccessDecisionClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new PlaybackDecisionHandler(this));
            services.AddHttpClient(MediaJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(_ => new JwksHandler(() => JwksDocument));
        });
    }

    public string CreateToken(Guid tenantId, string audience = "media", params string[] permissions)
        => CreateToken(
            tenantId,
            Guid.CreateVersion7(),
            audience,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            permissions);

    public string CreateTokenForActor(Guid tenantId, Guid actorAccountId, params string[] permissions)
        => CreateToken(
            tenantId,
            actorAccountId,
            "media",
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            permissions);

    public string CreateStudentToken(Guid tenantId, Guid studentId, string? email = "student@example.com", string audience = "media")
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", studentId.ToString("D")), new("tenantId", tenantId.ToString("D")),
            new("sessionId", Guid.CreateVersion7().ToString("D")), new("scope", "playback:use"),
        };
        if (email is not null) claims.Add(new("email", email));
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("identity", audience, claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new RsaSecurityKey(signingKey) { KeyId = SigningKeyId }, SecurityAlgorithms.RsaSha256));
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class PlaybackDecisionHandler(VideoLibraryApiFactory factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(request.Headers.Authorization!.Parameter);
            var tenant = Guid.Parse(token.Claims.Single(claim => claim.Type == "tenantId").Value);
            factory.PlaybackDecisionCalls.AddOrUpdate(tenant, 1, (_, calls) => calls + 1);
            factory.PlaybackDecisions.TryGetValue(tenant, out var status);
            if (status == 503) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = System.Net.Http.Json.JsonContent.Create(new
                {
                    decision = status == 403 ? "denied" : "allowed",
                    validity = status == 403 ? null : new { type = "lifetime" },
                    deniedReason = status == 403 ? "no-grant" : null,
                    decidedAt = DateTimeOffset.UtcNow,
                }),
            });
        }
    }

    public string CreateExpiredToken(Guid tenantId)
        => CreateToken(
            tenantId,
            Guid.CreateVersion7(),
            "media",
            DateTime.UtcNow.AddMinutes(-31),
            DateTime.UtcNow.AddMinutes(-30),
            ["midia.enviar"]);

    private string CreateToken(
        Guid tenantId,
        Guid actorAccountId,
        string audience,
        DateTime notBefore,
        DateTime expires,
        string[] permissions)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", actorAccountId.ToString("D")),
            new("tenantId", tenantId.ToString("D")),
            new("roles", "professor"),
            new("sessionId", Guid.CreateVersion7().ToString("D")),
            new("scope", "videos:write"),
        };
        claims.AddRange(permissions.Select(permission => new System.Security.Claims.Claim("permissions", permission)));
        var credentials = new SigningCredentials(
            new RsaSecurityKey(signingKey) { KeyId = SigningKeyId },
            SecurityAlgorithms.RsaSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            "identity",
            audience,
            claims,
            notBefore,
            expires,
            credentials);
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        signingKey.Dispose();
        await infrastructure.DisposeAsync();
    }

    private static string CreateJwksDocument(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kid = SigningKeyId,
                    kty = "RSA",
                    use = "sig",
                    alg = SecurityAlgorithms.RsaSha256,
                    n = Base64UrlEncode(parameters.Modulus!),
                    e = Base64UrlEncode(parameters.Exponent!),
                },
            },
        });
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class JwksHandler(Func<string> readDocument) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(readDocument()),
            });
    }
}

public sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
    private long utcTicks = initialUtcNow.UtcDateTime.Ticks;

    public override DateTimeOffset GetUtcNow()
        => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);

    public void SetUtcNow(DateTimeOffset value)
        => Interlocked.Exchange(ref utcTicks, value.UtcDateTime.Ticks);

    public void Advance(TimeSpan duration)
        => Interlocked.Add(ref utcTicks, duration.Ticks);
}

[CollectionDefinition(Name)]
public sealed class VideoLibraryApiCollection : ICollectionFixture<VideoLibraryApiFactory>
{
    public const string Name = "media-video-library-integration";
}
