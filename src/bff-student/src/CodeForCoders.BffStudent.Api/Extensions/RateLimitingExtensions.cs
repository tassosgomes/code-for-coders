using System.Globalization;
using System.Threading.RateLimiting;
using CodeForCoders.BffStudent.Application.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace CodeForCoders.BffStudent.Api.Extensions;

/// <summary>
/// The only place that calls AddRateLimiter. A second call would not merge: the last OnRejected wins silently.
/// OnRejected is therefore shared and picks the response by the policy attached to the rejected endpoint.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddBffRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        PurchaseIntentRateLimitExtensions.AddPurchaseIntentOptions(services, configuration);
        AnonymousRateLimitExtensions.AddAnonymousOptions(services, configuration);
        services.AddRateLimiter(options =>
        {
            PurchaseIntentRateLimitExtensions.AddPurchaseIntentPolicy(options);
            AnonymousRateLimitExtensions.AddAnonymousPolicy(options);
            options.OnRejected = RejectAsync;
        });
        return services;
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        string title;
        if (policy == AnonymousRateLimitExtensions.Policy)
        {
            BffStudentTelemetry.AnonymousRateLimited.Add(1);
            title = "Muitas requisições.";
        }
        else
        {
            BffStudentTelemetry.PurchaseIntentsRateLimited.Add(1);
            title = "Too many purchase intent requests.";
        }

        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        http.Response.Headers.CacheControl = "no-store";
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: title,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = "RATE_LIMITED",
                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? http.TraceIdentifier,
            }).ExecuteAsync(http);
    }
}
