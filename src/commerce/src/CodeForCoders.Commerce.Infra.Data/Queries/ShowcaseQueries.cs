using CodeForCoders.Commerce.Application.Interfaces;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class ShowcaseQueries(CommerceDbContext dbContext) : IShowcaseQueries
{
    private const int SummaryLength = 200;
    private const string Published = "published";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<ShowcaseCourseDetail?> GetCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        // Same predicate as the listing; the tenant filter of the context keeps another school's course out.
        var course = await dbContext.CatalogCourseViews.AsNoTracking()
            .Where(view => view.CourseId == courseId && view.InShowcaseSince != null && view.Level != null
                && view.Offers.Any(offer => offer.Status == Published))
            .Select(view => new
            {
                view.CourseId,
                view.Title,
                Level = view.Level!,
                view.Description,
                view.PrerequisiteJson,
                view.StructureJson,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (course is null)
        {
            return null;
        }

        var offers = await dbContext.CatalogOffers.AsNoTracking()
            .Where(offer => offer.CourseId == courseId && offer.Status == Published)
            .OrderBy(offer => offer.PriceCents).ThenBy(offer => offer.CreatedAt).ThenBy(offer => offer.OfferId)
            .Select(offer => new ShowcaseOffer(offer.OfferId, offer.Name, offer.PriceCents, offer.AccessPeriod))
            .ToListAsync(cancellationToken);
        var stored = JsonSerializer.Deserialize<StoredPrerequisite>(course.PrerequisiteJson, JsonOptions);
        var recommended = stored?.RecommendedCourses ?? [];
        var ids = recommended.Select(reference => reference.CourseId).ToArray();
        var known = await dbContext.CatalogCourseViews.AsNoTracking()
            .Where(view => ids.Contains(view.CourseId))
            .Select(view => new { view.CourseId, view.Title, InShowcase = view.InShowcaseSince != null })
            .ToDictionaryAsync(view => view.CourseId, cancellationToken);
        var prerequisite = new ShowcasePrerequisite(
            stored?.Text,
            recommended.Select(reference => known.TryGetValue(reference.CourseId, out var view)
                ? new ShowcaseRecommendedCourse(reference.CourseId, view.Title, view.InShowcase)
                : new ShowcaseRecommendedCourse(reference.CourseId, reference.Title, false)).ToArray());
        var modules = (JsonSerializer.Deserialize<StoredModule[]>(course.StructureJson, JsonOptions) ?? [])
            .OrderBy(module => module.Position)
            .Select(module => new ShowcaseModule(
                module.Title,
                module.Lessons.OrderBy(lesson => lesson.Position).Select(lesson => new ShowcaseLesson(lesson.Title)).ToArray()))
            .ToArray();
        return new ShowcaseCourseDetail(course.CourseId, course.Title, course.Level, course.Description, prerequisite, modules, offers);
    }

    private static string Summarize(string? tagline, string excerpt)
        => string.IsNullOrWhiteSpace(tagline) ? excerpt.Trim() : tagline;

    private sealed record StoredPrerequisite(string? Text, StoredReference[] RecommendedCourses);

    private sealed record StoredReference(Guid CourseId, string Title);

    private sealed record StoredModule(string Title, int Position, StoredLesson[] Lessons);

    private sealed record StoredLesson(string Title, int Position);
}
