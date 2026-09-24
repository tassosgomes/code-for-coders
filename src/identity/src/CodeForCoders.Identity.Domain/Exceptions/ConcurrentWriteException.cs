namespace CodeForCoders.Identity.Domain.Exceptions;

public sealed class ConcurrentWriteException(Exception innerException)
    : Exception("A concurrent identity write changed the same state.", innerException);
