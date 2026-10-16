using System.Threading.RateLimiting;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

/// <summary>Purchase-intent policy. Registered by <see cref="RateLimitingExtensions.AddBffRateLimiting"/>, the single owner of AddRateLimiter.</summary>
public static class PurchaseIntentRateLimitExtensions
{
    public const string Policy = "PurchaseIntentPerOffer";

    internal static void AddPurchaseIntentOptions(IServiceCollection services, IConfiguration configuration)
        => services.AddOptions<PurchaseIntentRateLimitOptions>().Bind(configuration.GetSection(PurchaseIntentRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0 && options.WindowSeconds > 0, "Purchase intent rate limits must be positive.")
            .ValidateOnStart();

    internal static void AddPurchaseIntentPolicy(RateLimiterOptions options)
        => options.AddPolicy(Policy, context =>
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
}
