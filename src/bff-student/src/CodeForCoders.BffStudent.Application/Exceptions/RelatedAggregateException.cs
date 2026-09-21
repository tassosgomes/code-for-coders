namespace CodeForCoders.BffStudent.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
