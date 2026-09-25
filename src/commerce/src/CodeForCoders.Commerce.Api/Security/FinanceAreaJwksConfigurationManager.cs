using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CodeForCoders.Commerce.Api.Security;

public sealed class FinanceAreaJwksConfigurationManager(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<FinanceAreaTokenOptions> options,
    TimeProvider timeProvider,
    ILogger<FinanceAreaJwksConfigurationManager> logger) : IConfigurationManager<OpenIdConnectConfiguration>
{
    public const string HttpClientName = "identity-jwks";

    private static readonly TimeSpan ConfigurationLifetime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureLifetime = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly object cacheLock = new();
    private OpenIdConnectConfiguration? cachedConfiguration;
    private DateTimeOffset expiresAt;
    private bool refreshRequested;

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedConfiguration(out var configuration))
        {
            return configuration;
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedConfiguration(out configuration))
            {
                return configuration;
            }

            var (nextConfiguration, lifetime) = await FetchConfigurationAsync(cancellationToken);
            lock (cacheLock)
            {
                cachedConfiguration = nextConfiguration;
                expiresAt = timeProvider.GetUtcNow().Add(lifetime);
                refreshRequested = false;
            }

            return nextConfiguration;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void RequestRefresh()
    {
        lock (cacheLock)
        {
            refreshRequested = true;
        }
    }

    private bool TryGetCachedConfiguration(out OpenIdConnectConfiguration configuration)
    {
        lock (cacheLock)
        {
            if (cachedConfiguration is not null
                && !refreshRequested
                && expiresAt > timeProvider.GetUtcNow())
            {
                configuration = cachedConfiguration;
                return true;
            }
        }

        configuration = new OpenIdConnectConfiguration();
        return false;
    }

    private async Task<(OpenIdConnectConfiguration Configuration, TimeSpan Lifetime)> FetchConfigurationAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync(options.CurrentValue.JwksUrl, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning("Identity JWKS endpoint returned {StatusCode}.", response.StatusCode);
                return (CreateEmptyConfiguration(), FailureLifetime);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var keySet = new JsonWebKeySet(json);
            var configuration = new OpenIdConnectConfiguration
            {
                Issuer = options.CurrentValue.Issuer,
            };
            foreach (var jsonWebKey in keySet.Keys)
            {
                if (jsonWebKey.Kty == "RSA"
                    && !string.IsNullOrWhiteSpace(jsonWebKey.Kid)
                    && (string.IsNullOrWhiteSpace(jsonWebKey.Use) || jsonWebKey.Use == "sig")
                    && (string.IsNullOrWhiteSpace(jsonWebKey.Alg) || jsonWebKey.Alg == SecurityAlgorithms.RsaSha256))
                {
                    configuration.SigningKeys.Add(jsonWebKey);
                }
            }

            return configuration.SigningKeys.Count == 0
                ? (CreateEmptyConfiguration(), FailureLifetime)
                : (configuration, ConfigurationLifetime);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not retrieve Identity signing keys.");
            return (CreateEmptyConfiguration(), FailureLifetime);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Identity returned an invalid JWKS document.");
            return (CreateEmptyConfiguration(), FailureLifetime);
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Identity returned an invalid JWKS document.");
            return (CreateEmptyConfiguration(), FailureLifetime);
        }
    }

    private static OpenIdConnectConfiguration CreateEmptyConfiguration()
        => new();
}
