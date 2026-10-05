using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class PurchaseOfferReader(CommerceDbContext db) : ICatalogPurchaseOfferQueries
{
    public async Task<PurchaseOffer?> FindAsync(Guid offerId, CancellationToken cancellationToken)
    {
        var offer = await db.CatalogOffers.AsNoTracking().SingleOrDefaultAsync(item => item.OfferId == offerId && item.Status == "published", cancellationToken);
        if (offer is null) return null;
        var course = await db.CatalogCourseViews.AsNoTracking().SingleOrDefaultAsync(item => item.CourseId == offer.CourseId, cancellationToken);
        return course is null ? null : new(course.CourseId, course.Title, offer.OfferId, offer.Name, offer.PriceCents, "BRL", offer.AccessPeriod);
    }
}
