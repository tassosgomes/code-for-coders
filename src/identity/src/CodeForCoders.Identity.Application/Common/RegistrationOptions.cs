namespace CodeForCoders.Identity.Application.Common;

public sealed class RegistrationOptions
{
    public const string SectionName = "StudentAccount";

    public string ConfirmationBaseUrl { get; set; } = string.Empty;

    public int ConfirmationLifetimeHours { get; set; } = 24;

    public string PasswordResetBaseUrl { get; set; } = string.Empty;

    public int PasswordResetLifetimeHours { get; set; } = 1;
}
