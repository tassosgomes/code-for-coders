using System.Threading.RateLimiting;
using CodeForCoders.BffStudent.Api.Captcha;
using CodeForCoders.BffStudent.Api.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CodeForCoders.BffStudent.Api.Extensions;

/// <summary>Per-IP policy for the anonymous endpoints. The partition is path + IP so login attempts do not eat the sign-up budget.</summary>
public static class AnonymousRateLimitExtensions
{
    public const string Policy = "AnonymousPerIp";
    private const string UnknownIp = "unknown";

    internal static void AddAnonymousOptions(IServiceCollection services, IConfiguration configuration)
        => services.AddOptions<AnonymousRateLimitOptions>().Bind(configuration.GetSection(AnonymousRateLimitOptions.SectionName))
            .Validate(options => options.PermitLimit > 0 && options.WindowSeconds > 0, "Anonymous rate limits must be positive.")
            .ValidateOnStart();

    internal static void AddAnonymousPolicy(RateLimiterOptions options)
        => options.AddPolicy(Policy, context =>
        {
            var settings = context.RequestServices.GetRequiredService<IOptions<AnonymousRateLimitOptions>>().Value;
            var key = $"{context.Request.Path.Value?.ToLowerInvariant()}|{ClientIpResolver.Resolve(context) ?? UnknownIp}";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.PermitLimit,
                Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });

    public static TBuilder RequireAnonymousRateLimit<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireRateLimiting(Policy);
}
