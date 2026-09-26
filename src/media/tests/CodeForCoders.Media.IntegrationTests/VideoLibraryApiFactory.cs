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
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class VideoLibraryApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string SigningKeyId = "media-integration";
    private readonly RSA signingKey = RSA.Create(2048);
    private readonly MediaIntegrationFixture infrastructure = new();

    public string JwksDocument { get; private set; } = string.Empty;

    public string MinioEndpoint => infrastructure.MinioEndpoint;

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

            services.AddHttpClient(MediaJwksConfigurationManager.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(_ => new JwksHandler(() => JwksDocument));
        });
    }

    public string CreateToken(Guid tenantId, string audience = "media", params string[] permissions)
        => CreateToken(
            tenantId,
            audience,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            permissions);

    public string CreateExpiredToken(Guid tenantId)
        => CreateToken(
            tenantId,
            "media",
            DateTime.UtcNow.AddMinutes(-31),
            DateTime.UtcNow.AddMinutes(-30),
            ["midia.enviar"]);

    private string CreateToken(
        Guid tenantId,
        string audience,
        DateTime notBefore,
        DateTime expires,
        string[] permissions)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", Guid.CreateVersion7().ToString("D")),
            new("tenantId", tenantId.ToString("D")),
            new("roles", "professor"),
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

[CollectionDefinition(Name)]
public sealed class VideoLibraryApiCollection : ICollectionFixture<VideoLibraryApiFactory>
{
    public const string Name = "media-video-library-integration";
}
