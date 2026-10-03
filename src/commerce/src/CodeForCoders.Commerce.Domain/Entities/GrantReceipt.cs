namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class GrantReceipt
{
    private GrantReceipt() { }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string KeyHash { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public string ResponseJson { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; }

    public static GrantReceipt Create(Guid tenantId, Guid actorId, string key)
        => new() { Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = actorId, KeyHash = key };

    public void Store(string requestHash, string responseJson, DateTimeOffset now)
    {
        RequestHash = requestHash;
        ResponseJson = responseJson;
        ExpiresAt = now.AddHours(24);
    }
}
