using CodeForCoders.BffStudent.Application;
using CodeForCoders.BffStudent.Api.Clients;
using CodeForCoders.BffStudent.Api.Security;
using CodeForCoders.BffStudent.Infra.Data;
using CodeForCoders.BffStudent.Infra.Messaging;
using Microsoft.Extensions.Http.Resilience;

namespace CodeForCoders.BffStudent.Api.Extensions;

public static class ServiceConfigurationExtensions
{
    public static WebApplicationBuilder AddBffStudentConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplicationConfiguration();
        builder.Services.AddDataConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddMessagingConfiguration(builder.Configuration);
        builder.Services.AddErrorHandlingConfiguration();
        builder.Services.AddHealthConfiguration();
        builder.Services.AddObservabilityConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddBffProxyConfiguration(builder.Configuration);
        builder.Services.AddOptions<StudentIdentityOptions>()
            .Bind(builder.Configuration.GetSection(StudentIdentityOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https", "Student Identity base address must be absolute HTTP(S).")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.Scope)
                && !string.IsNullOrWhiteSpace(options.SigningKeyId)
                && Guid.TryParse(options.TenantId, out var tenantId)
                && tenantId != Guid.Empty, "Student Identity assertion claims are incomplete.")
            .Validate(options => IsValidPrivateKey(options.SigningKeyBase64), "Student Identity signing key must be an RSA PKCS#8 key of at least 2048 bits.")
            .ValidateOnStart();
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddHttpClient<IStudentRegistrationIdentityClient, StudentRegistrationIdentityClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StudentIdentityOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseAddress);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        var allowedOrigins = builder.Configuration.GetSection(StudentSpaCorsOptions.SectionName)
            .Get<StudentSpaCorsOptions>()?.AllowedOrigins ?? [];
        builder.Services.AddOptions<StudentSpaCorsOptions>()
            .Bind(builder.Configuration.GetSection(StudentSpaCorsOptions.SectionName))
            .Validate(options => options.AllowedOrigins.Length > 0
                && options.AllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
                "At least one absolute Student SPA origin is required.")
            .ValidateOnStart();
        builder.Services.AddCors(options => options.AddPolicy("StudentSpa", policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        builder.Services.AddHttpClient("bff-student-outbound")
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
            });
        return builder;
    }

    private static bool IsValidPrivateKey(string encodedKey)
    {
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(encodedKey), out _);
            return rsa.KeySize >= 2048;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
