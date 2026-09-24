namespace CodeForCoders.Identity.Domain.Exceptions;

public sealed class RegistrationWriteConflictException(string constraintName, Exception innerException)
    : Exception("A student registration write conflicted with an existing record.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}
