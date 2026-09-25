namespace CodeForCoders.Identity.Application.Common;

public sealed class StaffAccountOptions
{
    public const string SectionName = "StaffAccount";

    public string PasswordResetBaseUrl { get; set; } = string.Empty;

    public int PasswordResetLifetimeHours { get; set; } = 1;
}
