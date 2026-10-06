namespace CodeForCoders.Billing.Infra.Gateway.Configuration;

public sealed class StripeGatewayOptions
{
    public string SecretKey { get; set; } = "";
    public string WebhookSigningSecret { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.stripe.com/";
}
