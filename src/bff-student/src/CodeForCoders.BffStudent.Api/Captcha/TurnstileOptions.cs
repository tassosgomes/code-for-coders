namespace CodeForCoders.BffStudent.Api.Captcha;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";
    public const string DefaultVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public bool Enabled { get; set; } = true;
    public string SecretKey { get; set; } = string.Empty;
    public string VerifyUrl { get; set; } = DefaultVerifyUrl;
    public int TimeoutSeconds { get; set; } = 5;
}
