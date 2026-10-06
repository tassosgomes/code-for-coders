using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeForCoders.Commerce.IntegrationTests;

/// <summary>Test-only adjustments shared by every real <c>commerce</c> host built by the integration tests.</summary>
public static class CommerceTestHost
{
    /// <summary>
    /// No OpenTelemetry collector runs in the tests. The pipeline stays exactly as in production, but with
    /// the default OTLP timeout every host shutdown waited 5–10 s for the final flush to an absent collector.
    /// </summary>
    public static void UseShortTelemetryExportTimeout(IWebHostBuilder builder)
        => builder.UseSetting("OpenTelemetry:ExportEnabled", "false");

    /// <summary>
    /// Points a named or typed client at a test double owned by the test. The handler never expires, so
    /// <c>IHttpClientFactory</c> does not dispose the double while a host is shared by several tests.
    /// </summary>
    public static IHttpClientBuilder UseHandler(IHttpClientBuilder client, HttpMessageHandler handler)
        => client.ConfigurePrimaryHttpMessageHandler(() => handler).SetHandlerLifetime(Timeout.InfiniteTimeSpan);

    /// <summary>Removes the hosted services declared by the commerce assemblies, keeping the framework ones.</summary>
    public static void RemoveCommerceWorkers(IServiceCollection services)
    {
        foreach (var worker in services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType?.Namespace?.StartsWith("CodeForCoders.Commerce.", StringComparison.Ordinal) == true)
            .ToList())
        {
            services.Remove(worker);
        }
    }
}
