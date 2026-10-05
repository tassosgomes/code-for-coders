namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record OrderCreation(long Number, OrderItem Item, DateTimeOffset Now);
