using CodeForCoders.Media.Application;
using CodeForCoders.Media.Api.Security;
using CodeForCoders.Media.Infra.Data;
using CodeForCoders.Media.Infra.Messaging;
using CodeForCoders.Media.Infra.Messaging.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddMediaConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddAccessDecisionConfiguration(builder.Configuration);
        if (MediaRoleOptions.ReadRole(builder.Configuration) == MediaServiceRole.Worker)
        {
            builder.Services.AddVideoPreparationConfiguration();
        }

        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddOptions<MediaTokenOptions>()
            .Bind(builder.Configuration.GetSection(MediaTokenOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https",
                "Media token validation settings are invalid.")
            .ValidateOnStart();
        builder.Services.AddHttpClient(MediaJwksConfigurationManager.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 1;
            });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<MediaJwksConfigurationManager>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, MediaJwtBearerOptionsSetup>();
        builder.Services.AddAuthorization(options => options.AddPolicy(
            MediaAuthorization.PolicyName,
            policy => policy.RequireAuthenticatedUser()
                .RequireClaim(MediaAuthorization.PermissionClaim, MediaAuthorization.RequiredPermission)));
        builder.Services.AddAuthorization(options => options.AddPolicy(
            MediaAuthorization.PlaybackPolicyName, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => Guid.TryParse(context.User.FindFirst("sessionId")?.Value, out var sessionId)
                    && sessionId != Guid.Empty && !context.User.HasClaim(claim => claim.Type == "permissions" || claim.Type == "roles")
                    && (context.User.FindFirst("scope")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("playback:use") ?? false))));
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddHttpClient("media-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
