using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required, Url]
    public string Endpoint { get; set; } = "https://email.example.invalid/v1/send";

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "notifications@example.invalid";

    [Required]
    public string SendingDomain { get; set; } = "example.invalid";

    public Dictionary<string, int> ValidityHoursByPurpose { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["confirmacao-de-conta"] = 24,
        ["recuperacao-de-senha"] = 1,
    };
}
