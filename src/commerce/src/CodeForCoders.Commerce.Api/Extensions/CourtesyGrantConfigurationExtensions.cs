using CodeForCoders.Commerce.Api.Clients;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Api.Extensions;

public static class CourtesyGrantConfigurationExtensions
{
    public static IServiceCollection AddCourtesyGrantConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StudentAccountIdentityOptions>().Bind(configuration.GetSection("StudentAccountIdentity"))
            .Validate(StudentAccountIdentityOptions.IsValid, "Commerce Identity signing key, issuer or school time zone is invalid.").ValidateOnStart();
        services.AddSingleton(provider => new SchoolTimeZone(TimeZoneInfo.FindSystemTimeZoneById(
            provider.GetRequiredService<IOptions<StudentAccountIdentityOptions>>().Value.SchoolTimeZone)));
        services.AddSingleton<StudentAccountAssertionTokenFactory>();
        // Confirmation has a short timeout and no retry: an unavailable account check fails closed.
        services.AddHttpClient<IStudentAccountConfirmationClient, StudentAccountConfirmationClient>((provider, client) =>
        {
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<StudentAccountIdentityOptions>>().Value.BaseAddress);
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        return services;
    }
}
