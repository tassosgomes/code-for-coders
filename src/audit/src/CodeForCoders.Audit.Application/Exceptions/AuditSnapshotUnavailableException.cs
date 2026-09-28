namespace CodeForCoders.Audit.Application.Exceptions;

public sealed class AuditSnapshotUnavailableException()
    : Exception("The audit search snapshot could not be stored.");
