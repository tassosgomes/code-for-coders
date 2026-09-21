namespace CodeForCoders.Notification.Application.Exceptions;

public sealed class RelatedAggregateException(string message) : UseCaseException(message);
