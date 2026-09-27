namespace CodeForCoders.Audit.Application.Exceptions;

public sealed class AuditFilterInvalidException : Exception
{
    public AuditFilterInvalidException()
        : base("The audit search filters or snapshot are invalid.")
    {
    }
}
