using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required, Url]
    public string Endpoint { get; set; } = "https://email.example.invalid/v1/send";

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "notifications@example.invalid";
}
