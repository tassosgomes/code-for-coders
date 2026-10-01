using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Domain.Events;

public sealed record OfferChanged(Guid EventId, Guid TenantId, Guid OfferId, Guid CourseId,
    int OfferRevision, DateTimeOffset OccurredAt, string Name, int PriceCents, string Currency,
    AccessPeriod AccessPeriod, OfferPreviousTerms Previous);

public sealed record OfferPreviousTerms(int PriceCents, AccessPeriod AccessPeriod);
