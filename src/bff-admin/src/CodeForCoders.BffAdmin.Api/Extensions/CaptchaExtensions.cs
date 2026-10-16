using CodeForCoders.BffAdmin.Api.Captcha;

namespace CodeForCoders.BffAdmin.Api.Extensions;

public static class CaptchaExtensions
{
    public static IServiceCollection AddCaptchaConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TurnstileOptions>()
            .Bind(configuration.GetSection(TurnstileOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SecretKey),
                "Turnstile secret key is required when the captcha is enabled.")
            .Validate(options => !options.Enabled
                || (Uri.TryCreate(options.VerifyUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"),
                "Turnstile verify URL must be absolute HTTP(S).")
            .Validate(options => options.TimeoutSeconds > 0, "Turnstile timeout must be positive.")
            .ValidateOnStart();
        services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TurnstileOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
        });
        return services;
    }
}
