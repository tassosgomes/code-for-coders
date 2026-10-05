namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class OrderReceipt
{
    private OrderReceipt() { }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public string KeyHash { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ResponseJson { get; private set; } = "";
    public int StatusCode { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public static OrderReceipt Create(Guid tenant, Guid student, string keyHash) => new() { TenantId = tenant, StudentId = student, KeyHash = keyHash };
    public void Store(string requestHash, OrderReceiptResponse response, DateTimeOffset now)
    { RequestHash = requestHash; ResponseJson = response.Json; StatusCode = response.StatusCode; ExpiresAt = now.AddHours(24); }
}
