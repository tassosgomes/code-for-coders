using CodeForCoders.Commerce.Domain.ValueObjects;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.Common;

public sealed record SummaryOffer(Guid OfferId, string Name, int PriceCents, string Currency, AccessPeriod AccessPeriod);
