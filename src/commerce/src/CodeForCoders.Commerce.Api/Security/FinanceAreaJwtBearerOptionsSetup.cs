using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CodeForCoders.Commerce.Api.Security;

public sealed class FinanceAreaJwtBearerOptionsSetup(
    FinanceAreaJwksConfigurationManager configurationManager,
    IOptions<FinanceAreaTokenOptions> tokenOptions) : IConfigureNamedOptions<JwtBearerOptions>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(JwtBearerOptions options)
        => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
        {
            return;
        }

        var settings = tokenOptions.Value;
        options.MapInboundClaims = false;
        options.ConfigurationManager = configurationManager;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteProblemAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "TOKEN_INVALID",
                    "Token de acesso inválido.");
            },
            OnForbidden = context => WriteProblemAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "PERMISSION_DENIED",
                "Sem permissão para esta área."),
        };
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            new FinanceAreaProblem(
                "about:blank",
                title,
                statusCode,
                code,
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier),
            JsonOptions,
            cancellationToken: context.RequestAborted);
    }

    private sealed record FinanceAreaProblem(string Type, string Title, int Status, string Code, string TraceId);
}
