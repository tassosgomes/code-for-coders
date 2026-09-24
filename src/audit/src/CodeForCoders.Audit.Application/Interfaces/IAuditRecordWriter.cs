using CodeForCoders.Audit.Domain.Entities;

namespace CodeForCoders.Audit.Application.Interfaces;

/// <summary>
/// Append-only persistence port. It intentionally has no update or delete operation.
/// </summary>
public interface IAuditRecordWriter
{
    Task AppendAsync(AuditRecord record, CancellationToken cancellationToken);

    Task<string?> ReadFingerprintAsync(string origin, Guid factId, CancellationToken cancellationToken);
}
