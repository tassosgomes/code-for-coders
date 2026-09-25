using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Identity.Application.Common;

public sealed class StaffSessionOptions
{
    public const string SectionName = "StaffSession";

    [Range(1, 1440)]
    public int InactivityTimeoutMinutes { get; set; } = 60;
}
