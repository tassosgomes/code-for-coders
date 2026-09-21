namespace CodeForCoders.Learning.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
