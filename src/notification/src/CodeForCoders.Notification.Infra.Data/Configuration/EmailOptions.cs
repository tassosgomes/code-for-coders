using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Notification.Infra.Data.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string Transport { get; set; } = "http";

    [Required, Url]
    public string Endpoint { get; set; } = "https://email.example.invalid/v1/send";

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "notifications@example.invalid";

    [Required]
    public string SendingDomain { get; set; } = "example.invalid";

    [Required]
    public string SmtpHost { get; set; } = "localhost";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 25;

    public bool SmtpEnableSsl { get; set; }

    public string? SmtpUsername { get; set; }

    public string? SmtpPassword { get; set; }

    public Dictionary<string, int> ValidityHoursByPurpose { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["confirmacao-de-conta"] = 24,
        ["recuperacao-de-senha"] = 1,
        ["convite-interno"] = 168,
    };
}
