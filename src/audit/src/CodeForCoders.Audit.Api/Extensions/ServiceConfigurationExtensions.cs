using CodeForCoders.Audit.Application;
using CodeForCoders.Audit.Api.Configuration;
using CodeForCoders.Audit.Infra.Data;
using CodeForCoders.Audit.Infra.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Audit.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddAuditConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddOptions<AuditTokensOptions>()
            .Bind(builder.Configuration.GetSection(AuditTokensOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.MetadataAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.Scope),
                "Audit JWT metadata, issuer, audience and scope configuration are required.")
            .ValidateOnStart();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuditTokensOptions>>((jwtOptions, auditOptions) =>
            {
                var settings = auditOptions.Value;
                jwtOptions.MetadataAddress = settings.MetadataAddress;
                jwtOptions.RequireHttpsMetadata = false;
                jwtOptions.MapInboundClaims = false;
                jwtOptions.RefreshOnIssuerKeyNotFound = true;
                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = ["RS256"],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "roles",
                };
                jwtOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Staff token is invalid.",
                            extensions: new Dictionary<string, object?>
                            {
                                ["code"] = "TOKEN_INVALID",
                                ["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier,
                            }).ExecuteAsync(context.HttpContext);
                    },
                };
            });
        builder.Services.AddAuthorization();
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        return builder;
    }
}
