namespace CodeForCoders.Notification.Api.Clients;

public sealed class StudentContactIdentityOptions
{
    public string BaseUrl { get; set; } = "http://identity:8080/";
    public string Issuer { get; set; } = "notification";
    public string Audience { get; set; } = "identity-internal";
    public string SigningKeyId { get; set; } = "local-notification-identity-1";
    public string SigningKeyBase64 { get; set; } = string.Empty;
}
