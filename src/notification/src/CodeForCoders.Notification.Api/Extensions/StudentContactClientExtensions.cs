using System.Security.Cryptography;
using CodeForCoders.Notification.Api.Clients;
using CodeForCoders.Notification.Api.Security;
using CodeForCoders.Notification.Application.Interfaces;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Api.Extensions;

public static class StudentContactClientExtensions
{
    public static IServiceCollection AddStudentContactClientConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StudentContactIdentityOptions>().Bind(configuration.GetSection("StudentContactIdentity"))
            .Validate(options => options.Issuer == "notification" && options.Audience == "identity-internal"
                && Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(options.SigningKeyId)
                && ValidKey(options.SigningKeyBase64), "Student contact client configuration is invalid.").ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<StudentContactAssertionTokenFactory>();
        services.AddTransient<StudentContactAssertionHandler>();
        services.AddHttpClient<IStudentContactClient, StudentContactClient>((provider, client) =>
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<StudentContactIdentityOptions>>().Value.BaseUrl))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddHttpClient<IStudentContactClient, StudentContactClient>().AddHttpMessageHandler<StudentContactAssertionHandler>();
        return services;
    }

    private static bool ValidKey(string value)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(value), out _);
            return rsa.KeySize >= 2048;
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
    }
}
