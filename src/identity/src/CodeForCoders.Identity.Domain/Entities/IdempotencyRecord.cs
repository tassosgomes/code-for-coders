using CodeForCoders.Identity.Domain.SeedWork;

namespace CodeForCoders.Identity.Domain.Entities;

public sealed class IdempotencyRecord
{
    private IdempotencyRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string OperationId { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public string Fingerprint { get; private set; } = string.Empty;

    public int StatusCode { get; private set; }

    public string? Code { get; private set; }

    public string? Title { get; private set; }

    public Guid? StudentSessionId { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset ExpiresOn { get; private set; }

    public bool IsExpired(DateTimeOffset now) => ExpiresOn <= now;

    public static IdempotencyRecord Create(
        Guid tenantId,
        string operationId,
        string keyHash,
        string fingerprint,
        int statusCode,
        string? code,
        string? title,
        DateTimeOffset createdOn,
        DateTimeOffset expiresOn)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(operationId)
            || string.IsNullOrWhiteSpace(keyHash) || string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new EntityValidationException("Idempotency data is incomplete.");
        }

        var record = new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(createdOn),
            TenantId = tenantId,
            OperationId = operationId,
            KeyHash = keyHash,
        };
        record.Refresh(fingerprint, statusCode, code, title, createdOn, expiresOn);
        return record;
    }

    public void Refresh(
        string fingerprint,
        int statusCode,
        string? code,
        string? title,
        DateTimeOffset createdOn,
        DateTimeOffset expiresOn)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || expiresOn <= createdOn)
        {
            throw new EntityValidationException("Idempotency result data is invalid.");
        }

        Fingerprint = fingerprint;
        StatusCode = statusCode;
        Code = code;
        Title = title;
        StudentSessionId = null;
        CreatedOn = createdOn;
        ExpiresOn = expiresOn;
    }

    public void SetStudentSessionId(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new EntityValidationException("The student session identifier is required.");
        }

        StudentSessionId = sessionId;
    }
}
