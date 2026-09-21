namespace CodeForCoders.Media.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
