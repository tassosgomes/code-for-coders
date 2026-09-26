namespace CodeForCoders.Identity.Application.Common;

public sealed class StaffInvitationOptions
{
    public const string SectionName = "StaffInvitation";

    public string AcceptanceBaseUrl { get; set; } = string.Empty;

    public int LifetimeHours { get; set; } = 168;
}
