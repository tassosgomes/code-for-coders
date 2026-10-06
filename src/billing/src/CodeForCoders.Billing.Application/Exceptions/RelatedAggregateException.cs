namespace CodeForCoders.Billing.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
