using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Application.Common;

public sealed class NotificationProcessingOptions
{
    public const string SectionName = "Notification";
    public const string DefaultNamespace = "default";

    [Required]
    public string Namespace { get; set; } = DefaultNamespace;
}
