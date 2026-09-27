using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Media.Infra.Messaging.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string Exchange { get; set; } = "media.events";

    [Required]
    public string DeadLetterExchange { get; set; } = "media.events.dlx";

    [Required]
    public string HeartbeatQueue { get; set; } = "media.platform-heartbeat";

    [Required]
    public string AuditQueue { get; set; } = "media.events.audit";

    [Range(1, int.MaxValue)]
    public int AuditMessageTtlMilliseconds { get; set; } = 604800000;

    [Range(1, int.MaxValue)]
    public int AuditMaxLength { get; set; } = 100000;

    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;

    [Range(1, 100)]
    public int DeliveryLimit { get; set; } = 5;
}
