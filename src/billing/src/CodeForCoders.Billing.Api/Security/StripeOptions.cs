namespace CodeForCoders.Billing.Api.Security;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSigningSecret { get; set; } = string.Empty;
}
