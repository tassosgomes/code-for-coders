using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.Entities;
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

[Collection(VideoLibraryApiCollection.Name)]
public sealed class VideoLibraryAuthorizationTests(VideoLibraryApiFactory factory)
{
    [Fact(DisplayName = nameof(VideoLibrary_ReturnsEmptyPageForAuthorizedTenant))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_ReturnsEmptyPageForAuthorizedTenant()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        using var request = AuthorizedRequest("/internal/v1/videos", tenantId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(document.RootElement.GetProperty("data").EnumerateArray());
        Assert.Equal(1, document.RootElement.GetProperty("pagination").GetProperty("page").GetInt32());
        Assert.Equal(10, document.RootElement.GetProperty("pagination").GetProperty("size").GetInt32());
    }

    [Fact(DisplayName = nameof(VideoLibrary_FiltersByTenantAndOrdersByUploadTime))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_FiltersByTenantAndOrdersByUploadTime()
    {
        var tenantId = Guid.CreateVersion7();
        var otherTenantId = Guid.CreateVersion7();
        var uploadedBy = Guid.CreateVersion7();
        var firstAt = new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero);
        await SeedAsync(
            Video.Create(tenantId, "Aula antiga", uploadedBy, "Professora", firstAt),
            Video.Create(tenantId, "Aula recente", uploadedBy, "Professora", firstAt.AddMinutes(1)),
            Video.Create(otherTenantId, "Outro tenant", uploadedBy, "Professora", firstAt.AddMinutes(2)));
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest("/internal/v1/videos?_page=1&_size=1", tenantId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var document = await ReadJsonAsync(response);
        var data = document.RootElement.GetProperty("data").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Aula recente", data.Single().GetProperty("title").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsRequestWithoutToken))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsRequestWithoutToken()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/internal/v1/videos", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_INVALID", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsExpiredToken))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsExpiredToken()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/videos");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateExpiredToken(Guid.CreateVersion7()));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_INVALID", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsTokenWithCommerceAudience))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsTokenWithCommerceAudience()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        using var request = AuthorizedRequest("/internal/v1/videos", tenantId, audience: "commerce");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_INVALID", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsTokenWithoutSendPermission))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsTokenWithoutSendPermission()
    {
        using var client = factory.CreateClient();
        var tenantId = Guid.CreateVersion7();
        using var request = AuthorizedRequest("/internal/v1/videos", tenantId, permissions: ["autoria.ler"]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("PERMISSION_DENIED", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_HidesVideoFromAnotherTenant))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_HidesVideoFromAnotherTenant()
    {
        var ownerTenantId = Guid.CreateVersion7();
        var requesterTenantId = Guid.CreateVersion7();
        var video = Video.Create(
            ownerTenantId,
            "Aula privada",
            Guid.CreateVersion7(),
            "Professora",
            new DateTimeOffset(2026, 9, 26, 8, 0, 0, TimeSpan.Zero));
        await SeedAsync(video);
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest($"/internal/v1/videos/{video.VideoId:D}", requesterTenantId);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("VIDEO_NOT_FOUND", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsPageSizeAboveContractLimit))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsPageSizeAboveContractLimit()
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest("/internal/v1/videos?_page=1&_size=51", Guid.CreateVersion7());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_REQUEST", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_RejectsForgedSignature))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_RejectsForgedSignature()
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest("/internal/v1/videos", Guid.CreateVersion7());
        var token = request.Headers.Authorization!.Parameter!;
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            string.Concat(token.AsSpan(0, token.Length - 1), token[^1] == 'A' ? "B" : "A"));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_INVALID", await ReadCodeAsync(response));
    }

    [Fact(DisplayName = nameof(VideoLibrary_FailsClosedWhenJwksIsUnavailableAndKeepsLivenessHealthy))]
    [Trait("Layer", "Media video library - Integration")]
    public async Task VideoLibrary_FailsClosedWhenJwksIsUnavailableAndKeepsLivenessHealthy()
    {
        using var unavailableFactory = new MediaJwksUnavailableApiFactory();
        using var client = unavailableFactory.CreateClient();
        using var livenessResponse = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/internal/v1/videos");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            MediaJwksUnavailableApiFactory.CreateToken(Guid.CreateVersion7()));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("TOKEN_INVALID", await ReadCodeAsync(response));
    }

    private async Task SeedAsync(params Video[] videos)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(videos[0].TenantId);
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        dbContext.Videos.AddRange(videos);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private HttpRequestMessage AuthorizedRequest(
        string path,
        Guid tenantId,
        string audience = "media",
        string[]? permissions = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            factory.CreateToken(tenantId, audience, permissions ?? ["midia.enviar"]));
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken),
            cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = await ReadJsonAsync(response);
        return document.RootElement.GetProperty("code").GetString();
    }

    private sealed class MediaJwksUnavailableApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTest");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=1;Database=unavailable");
            builder.UseSetting("Media:Role", "api");
            builder.UseSetting("RabbitMq:Username", "integration-test");
            builder.UseSetting("RabbitMq:Password", "integration-test");
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
                    .ConfigurePrimaryHttpMessageHandler(_ => new UnavailableJwksHandler());
            });
        }

        public static string CreateToken(Guid tenantId)
        {
            using var signingKey = RSA.Create(2048);
            var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
                "identity",
                "media",
                [
                    new System.Security.Claims.Claim("sub", Guid.CreateVersion7().ToString("D")),
                    new System.Security.Claims.Claim("tenantId", tenantId.ToString("D")),
                    new System.Security.Claims.Claim("permissions", "midia.enviar"),
                ],
                DateTime.UtcNow.AddMinutes(-1),
                DateTime.UtcNow.AddMinutes(5),
                new SigningCredentials(
                    new RsaSecurityKey(signingKey) { KeyId = "jwks-unavailable" },
                    SecurityAlgorithms.RsaSha256));
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        }

        private sealed class UnavailableJwksHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
