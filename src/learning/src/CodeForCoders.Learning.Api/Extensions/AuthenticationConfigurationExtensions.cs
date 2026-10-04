using CodeForCoders.Learning.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Learning.Api.Extensions;

public static class AuthenticationConfigurationExtensions
{
    public static IServiceCollection AddCourseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LearningTokenOptions>()
            .Bind(configuration.GetSection(LearningTokenOptions.SectionName))
            .Validate(options => options.Issuer.Length > 0 && options.Audience == "learning"
                && Uri.TryCreate(options.JwksUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https",
                "Learning token settings are invalid.").ValidateOnStart();
        services.AddHttpClient(LearningJwksConfigurationManager.HttpClientName)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.Retry.MaxRetryAttempts = 1;
            });
        services.AddSingleton<LearningJwksConfigurationManager>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, LearningJwtBearerOptionsSetup>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(LearningAuthorization.Student, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => !context.User.HasClaim(claim => claim.Type == "permissions")
                    && context.User.FindAll("scope").Any(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("lessons:read"))
                    && Guid.TryParse(context.User.FindFirst("sessionId")?.Value, out var sessionId) && sessionId != Guid.Empty));
            options.AddPolicy(LearningAuthorization.Administrator, policy => policy.RequireAuthenticatedUser().RequireClaim("roles", "administrador").RequireClaim("permissions"));
            options.AddPolicy(LearningAuthorization.Read, policy => policy.RequireAuthenticatedUser().RequireClaim("permissions", "autoria.ler"));
            options.AddPolicy(LearningAuthorization.Edit, policy => policy.RequireAuthenticatedUser().RequireClaim("permissions", "autoria.editar"));
        });
        return services;
    }
}
