using System.Globalization;
using System.Threading.RateLimiting;
using CodeForCoders.BffAdmin.Application.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace CodeForCoders.BffAdmin.Api.Extensions;

/// <summary>The only place that calls AddRateLimiter, so a later policy cannot silently replace this OnRejected.</summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddBffRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        AnonymousRateLimitExtensions.AddAnonymousOptions(services, configuration);
        services.AddRateLimiter(options =>
        {
            AnonymousRateLimitExtensions.AddAnonymousPolicy(options);
            options.OnRejected = RejectAsync;
        });
        return services;
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        BffAdminTelemetry.AnonymousRateLimited.Add(1);
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        http.Response.Headers.CacheControl = "no-store";
        await Results.Problem(
            type: "about:blank",
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Muitas requisições.",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "RATE_LIMITED",
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            }).ExecuteAsync(http);
    }
}
