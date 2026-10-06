using CodeForCoders.Commerce.Domain.ValueObjects;
namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record PurchaseOffer(Guid CourseId, string Title, Guid OfferId, string Name, int PriceCents, string Currency, AccessPeriod AccessPeriod);
