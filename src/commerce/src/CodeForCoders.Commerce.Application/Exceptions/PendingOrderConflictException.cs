namespace CodeForCoders.Commerce.Application.Exceptions;

public sealed class PendingOrderConflictException(Exception inner) : Exception("A pending order already exists.", inner);
