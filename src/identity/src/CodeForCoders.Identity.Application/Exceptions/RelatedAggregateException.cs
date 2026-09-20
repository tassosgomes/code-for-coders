namespace CodeForCoders.Identity.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
