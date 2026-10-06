namespace CodeForCoders.Commerce.Domain.Entities;

public sealed record PurchaseGrantDetails(Guid OrderId, string PeriodType, int? Months, DateTimeOffset Now);
