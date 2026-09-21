namespace CodeForCoders.Commerce.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
