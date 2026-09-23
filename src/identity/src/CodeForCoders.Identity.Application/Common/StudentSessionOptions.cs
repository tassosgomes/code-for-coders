using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Identity.Application.Common;

public sealed class StudentSessionOptions
{
    public const string SectionName = "StudentSession";

    [Range(1, 1440)]
    public int InactivityTimeoutMinutes { get; set; } = 60;
}
