using CodeForCoders.Commerce.Application;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddCommerceConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddOptions<FinanceAreaTokenOptions>()
            .Bind(builder.Configuration.GetSection(FinanceAreaTokenOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https",
                "Finance area token validation settings are invalid.")
            .ValidateOnStart();
        builder.Services.AddHttpClient(FinanceAreaJwksConfigurationManager.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 1;
            });
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<FinanceAreaJwksConfigurationManager>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, FinanceAreaJwtBearerOptionsSetup>();
        builder.Services.AddAuthorization(options => options.AddPolicy(
            FinanceAreaAuthorization.PolicyName,
            policy => policy.RequireAuthenticatedUser()
                .RequireClaim(FinanceAreaAuthorization.PermissionClaim, FinanceAreaAuthorization.RequiredPermission)));
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddHttpClient("commerce-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }
}
