namespace CodeForCoders.Billing.Api.Security;

public sealed class PaymentReturnOptions
{
    public const string SectionName = "PaymentReturn";

    public string[] AllowedHosts { get; set; } = [];
}
