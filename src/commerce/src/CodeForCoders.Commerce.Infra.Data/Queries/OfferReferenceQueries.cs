using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class OfferReferenceQueries(CommerceDbContext dbContext) : IOfferReferenceQueries
{
    public async Task<OfferReferenceList> ResolveAsync(IReadOnlyCollection<Guid> offerIds, CancellationToken cancellationToken)
    {
        var references = await (from offer in dbContext.CatalogOffers.AsNoTracking()
                                join course in dbContext.CatalogCourseViews.AsNoTracking() on offer.CourseId equals course.CourseId
                                where offerIds.Contains(offer.OfferId)
                                orderby offer.OfferId
                                select new OfferReference(offer.OfferId, course.Title + " — " + offer.Name))
            .ToListAsync(cancellationToken);
        return new(references);
    }
}
