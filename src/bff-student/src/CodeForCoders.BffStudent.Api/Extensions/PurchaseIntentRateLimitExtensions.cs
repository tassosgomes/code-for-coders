using System.Globalization;
using System.Threading.RateLimiting;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class PurchaseIntentRateLimitExtensions
{
    public const string Policy = "PurchaseIntentPerOffer";

    public static IServiceCollection AddPurchaseIntentRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PurchaseIntentRateLimitOptions>().Bind(configuration.GetSection(PurchaseIntentRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0 && options.WindowSeconds > 0, "Purchase intent rate limits must be positive.")
            .ValidateOnStart();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Policy, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<PurchaseIntentRateLimitOptions>>().Value;
                var offer = Guid.TryParse(context.Request.RouteValues["offerId"]?.ToString(), out var id) ? id : Guid.Empty;
                return RateLimitPartition.GetFixedWindowLimiter(offer, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                CodeForCoders.BffStudent.Application.Common.BffStudentTelemetry.PurchaseIntentsRateLimited.Add(1);
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 1;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many purchase intent requests.",
                    extensions: new Dictionary<string, object?> { ["code"] = "RATE_LIMITED", ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier })
                    .ExecuteAsync(context.HttpContext);
            };
        });
        return services;
    }
}
