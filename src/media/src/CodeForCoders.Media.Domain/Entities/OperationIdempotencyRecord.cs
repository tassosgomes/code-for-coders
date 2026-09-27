namespace CodeForCoders.Media.Domain.Entities;

public sealed class OperationIdempotencyRecord
{
    private OperationIdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ActorAccountId { get; private set; }

    public string Operation { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public int ResponseStatusCode { get; private set; }

    public string ResponseJson { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public static OperationIdempotencyRecord Create(
        Guid tenantId,
        Guid actorAccountId,
        string operation,
        string key,
        string requestHash,
        DateTimeOffset expiresAt)
        => new()
        {
            Id = Guid.CreateVersion7(expiresAt),
            TenantId = tenantId,
            ActorAccountId = actorAccountId,
            Operation = operation,
            Key = key,
            RequestHash = requestHash,
            ExpiresAt = expiresAt,
        };

    public void SetResponse(int statusCode, string responseJson, DateTimeOffset expiresAt)
    {
        ResponseStatusCode = statusCode;
        ResponseJson = responseJson;
        ExpiresAt = expiresAt;
    }

    public void Reuse(string requestHash, DateTimeOffset expiresAt)
    {
        RequestHash = requestHash;
        ResponseStatusCode = 0;
        ResponseJson = string.Empty;
        ExpiresAt = expiresAt;
    }
}
