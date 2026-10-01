using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class ShowcaseQueries(CommerceDbContext dbContext) : IShowcaseQueries
{
    private const int SummaryLength = 200;
    private const string Published = "published";

    public async Task<ShowcaseCoursePage> ListAsync(string? level, int page, int size, CancellationToken cancellationToken)
    {
        // Same predicate as CatalogCourseView.IsShowcaseEligible: a level and at least one published offer.
        var query = dbContext.CatalogCourseViews.AsNoTracking()
            .Where(course => course.InShowcaseSince != null && course.Level != null
                && course.Offers.Any(offer => offer.Status == Published));
        if (level is not null)
        {
            query = query.Where(course => course.Level == level);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(course => course.InShowcaseSince).ThenBy(course => course.CourseId)
            .Skip((page - 1) * size).Take(size)
            .Select(course => new
            {
                course.CourseId,
                course.Title,
                Level = course.Level!,
                course.Tagline,
                Excerpt = course.Description.Substring(0, SummaryLength),
                LowestPriceCents = course.Offers.Where(offer => offer.Status == Published).Min(offer => offer.PriceCents),
                OfferCount = course.Offers.Count(offer => offer.Status == Published),
            })
            .ToListAsync(cancellationToken);
        var cards = rows.Select(row => new ShowcaseCourseCard(
            row.CourseId, row.Title, row.Level, Summarize(row.Tagline, row.Excerpt), row.LowestPriceCents, row.OfferCount)).ToArray();
        return new ShowcaseCoursePage(cards, new CatalogPagination(page, size, total, (int)Math.Ceiling((double)total / size)));
    }

    private static string Summarize(string? tagline, string excerpt)
        => string.IsNullOrWhiteSpace(tagline) ? excerpt.Trim() : tagline;
}
