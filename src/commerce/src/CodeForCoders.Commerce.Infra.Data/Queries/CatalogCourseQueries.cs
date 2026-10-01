using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class CatalogCourseQueries(CommerceDbContext dbContext) : ICatalogCourseQueries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogCourseDetail?> GetAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await dbContext.CatalogCourseViews.AsNoTracking().Where(course => course.CourseId == courseId)
            .Select(course => new
            {
                course.CourseId,
                course.Title,
                course.Level,
                course.PrerequisiteJson,
                course.Tagline,
                course.InShowcaseSince
            }).SingleOrDefaultAsync(cancellationToken);
        if (course is null) return null;
        var prerequisite = JsonSerializer.Deserialize<CatalogPrerequisite>(course.PrerequisiteJson, JsonOptions)
            ?? new CatalogPrerequisite(null, []);
        var ids = prerequisite.RecommendedCourses.Select(reference => reference.CourseId).ToArray();
        var titles = await dbContext.CatalogCourseViews.AsNoTracking().Where(reference => ids.Contains(reference.CourseId))
            .ToDictionaryAsync(reference => reference.CourseId, reference => reference.Title, cancellationToken);
        var resolved = prerequisite with
        {
            RecommendedCourses = prerequisite.RecommendedCourses
            .Select(reference => reference with { Title = titles.GetValueOrDefault(reference.CourseId, reference.Title) }).ToArray()
        };
        var offers = await dbContext.CatalogOffers.AsNoTracking().Where(offer => offer.CourseId == courseId)
            .OrderBy(offer => offer.Status == "draft" ? 0 : offer.Status == "published" ? 1 : 2)
            .ThenBy(offer => offer.CreatedAt).ThenBy(offer => offer.OfferId).ToListAsync(cancellationToken);
        var counts = await dbContext.PurchaseIntentDailyCounts.AsNoTracking()
            .Where(item => offers.Select(offer => offer.OfferId).Contains(item.OfferId))
            .GroupBy(item => item.OfferId).Select(group => new { OfferId = group.Key, Count = group.Sum(item => item.Count) })
            .ToDictionaryAsync(item => item.OfferId, item => item.Count, cancellationToken);
        return new(course.CourseId, course.Title, course.Level, resolved, course.Tagline, course.InShowcaseSince.HasValue, offers.Select(offer => CatalogOfferDetail.FromCatalogOffer(offer) with { PurchaseIntentCount = counts.GetValueOrDefault(offer.OfferId) }).ToArray());
    }

    public async Task<CatalogCoursePage> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var query = dbContext.CatalogCourseViews.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var courses = await query.OrderBy(course => course.Title).ThenBy(course => course.CourseId)
            .Skip((page - 1) * size).Take(size)
            .Select(course => new CatalogCourseSummary(course.CourseId, course.Title, course.Level,
                course.InShowcaseSince != null, new CatalogOfferCounts(course.Offers.Count(offer => offer.Status == "draft"),
                    course.Offers.Count(offer => offer.Status == "published"), course.Offers.Count(offer => offer.Status == "unpublished"))))
            .ToListAsync(cancellationToken);
        return new CatalogCoursePage(courses, new CatalogPagination(page, size, total, (int)Math.Ceiling((double)total / size)));
    }
}
