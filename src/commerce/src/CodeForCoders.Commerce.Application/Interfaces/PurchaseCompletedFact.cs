using CodeForCoders.Commerce.Domain.ValueObjects;
namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PurchaseCompletedFact(Guid EventId, Guid TenantId, Guid OrderId, string OrderNumber, Guid StudentId,
 Guid CourseId, Guid OfferId, int PriceCents, int PaidAmountCents, string Currency, AccessPeriod AccessPeriod,
 string PaymentMethod, DateTimeOffset PaidAt, DateTimeOffset OccurredAt);
