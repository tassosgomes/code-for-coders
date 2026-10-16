namespace CodeForCoders.BffAdmin.Api.Security;

/// <summary>Per-IP limit for the anonymous endpoints covered by the captcha (login, sign-up, password-reset request).</summary>
public sealed class AnonymousRateLimitOptions
{
    public const string SectionName = "AnonymousRateLimit";
    public int PermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}
