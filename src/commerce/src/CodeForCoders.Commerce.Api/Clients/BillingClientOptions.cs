namespace CodeForCoders.Commerce.Api.Clients;

public sealed class BillingClientOptions
{
    public string BaseUrl { get; set; } = "http://billing:8080";
    public string Issuer { get; set; } = "commerce";
    public string Audience { get; set; } = "billing";
    public string SigningKeyId { get; set; } = "";
    public string SigningKeyBase64 { get; set; } = "";
}
