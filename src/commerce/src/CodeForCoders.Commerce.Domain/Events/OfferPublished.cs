using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Domain.Events;

public sealed record OfferPublished(Guid EventId, Guid TenantId, Guid OfferId, Guid CourseId,
    int OfferRevision, DateTimeOffset OccurredAt, string Name, int PriceCents, string Currency, AccessPeriod AccessPeriod);
