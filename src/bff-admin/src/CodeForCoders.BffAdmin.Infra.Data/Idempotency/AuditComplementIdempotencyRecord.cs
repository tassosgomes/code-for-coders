namespace CodeForCoders.BffAdmin.Infra.Data.Idempotency;

public sealed class AuditComplementIdempotencyRecord
{
    public const string Operation = "confirm-audit-record-complement";

    private AuditComplementIdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ActorId { get; private set; }

    public string OperationId { get; private set; } = Operation;

    public Guid IdempotencyKey { get; private set; }

    public byte[] RequestHash { get; private set; } = [];

    public Guid ConfirmationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public static AuditComplementIdempotencyRecord Create(
        Guid tenantId,
        Guid actorId,
        Guid idempotencyKey,
        byte[] requestHash,
        Guid confirmationId,
        DateTimeOffset createdAt)
    {
        var record = new AuditComplementIdempotencyRecord
        {
            Id = Guid.CreateVersion7(createdAt),
            TenantId = tenantId,
            ActorId = actorId,
            OperationId = Operation,
            IdempotencyKey = idempotencyKey,
        };
        record.Refresh(requestHash, confirmationId, createdAt);
        return record;
    }

    public void Refresh(byte[] requestHash, Guid confirmationId, DateTimeOffset createdAt)
    {
        RequestHash = requestHash;
        ConfirmationId = confirmationId;
        CreatedAt = createdAt;
        ExpiresAt = createdAt.AddHours(24);
    }
}
