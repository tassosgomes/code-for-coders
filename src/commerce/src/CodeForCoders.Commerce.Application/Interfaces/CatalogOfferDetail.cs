using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record CatalogOfferDetail(Guid OfferId, Guid CourseId, string Name, int PriceCents,
    AccessPeriod AccessPeriod, string Status, long PurchaseIntentCount, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? PublishedAt)
{
    public static CatalogOfferDetail FromCatalogOffer(CatalogOffer offer)
        => new(offer.OfferId, offer.CourseId, offer.Name, offer.PriceCents, offer.AccessPeriod, offer.Status,
            0, offer.CreatedAt, offer.UpdatedAt, offer.PublishedAt);
}
