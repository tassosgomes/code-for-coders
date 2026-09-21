namespace CodeForCoders.BffAdmin.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
