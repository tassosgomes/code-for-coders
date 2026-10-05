using System.ComponentModel.DataAnnotations;

namespace CodeForCoders.Commerce.Infra.Messaging.Configuration;

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
    public string Exchange { get; set; } = "commerce.events";

    [Required]
    public string AuditExchange { get; set; } = "audit.events";

    [Required]
    public string EntitlementFactRetentionQueue { get; set; } = "commerce.entitlement-fact-retention";
    [Range(1, int.MaxValue)]
    public int EntitlementFactRetentionMaxLength { get; set; } = 10000;
    [Range(1, int.MaxValue)]
    public int EntitlementFactRetentionTtlMilliseconds { get; set; } = 86400000;

    [Required]
    public string OfferRetentionQueue { get; set; } = "commerce.catalog-offer-retention";

    [Range(1, int.MaxValue)]
    public int OfferRetentionMaxLength { get; set; } = 10000;

    [Range(1, int.MaxValue)]
    public int OfferRetentionTtlMilliseconds { get; set; } = 604800000;

    [Required]
    public string DeadLetterExchange { get; set; } = "commerce.events.dlx";

    [Required]
    public string HeartbeatQueue { get; set; } = "commerce.platform-heartbeat";

    [Required]
    public string LearningExchange { get; set; } = "learning.events";

    [Required]
    public string CatalogCourseQueue { get; set; } = "commerce.catalog-course";

    [Required]
    public string EntitlementCourseQueue { get; set; } = "commerce.entitlement-course";

    [Required] public string BillingExchange { get; set; } = "billing.events";
    [Required] public string SalesPaymentsQueue { get; set; } = "commerce.sales-payments";
    [Required] public string EntitlementPurchasesQueue { get; set; } = "commerce.entitlement-purchases";
    [Required] public string SalesAccessGrantedQueue { get; set; } = "commerce.sales-access-granted";

    [Range(1, 1000)]
    public ushort PrefetchCount { get; set; } = 10;

    [Range(1, 100)]
    public int DeliveryLimit { get; set; } = 5;
}
