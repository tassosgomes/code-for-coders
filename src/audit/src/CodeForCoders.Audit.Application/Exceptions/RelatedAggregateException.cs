namespace CodeForCoders.Audit.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
