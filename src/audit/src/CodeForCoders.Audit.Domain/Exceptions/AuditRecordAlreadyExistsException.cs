namespace CodeForCoders.Audit.Domain.Exceptions;

public sealed class AuditRecordAlreadyExistsException()
    : Exception("An audit record with the same origin and fact ID already exists.")
{
}
