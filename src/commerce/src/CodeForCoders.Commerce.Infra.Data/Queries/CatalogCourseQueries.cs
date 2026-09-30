using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class CatalogCourseQueries(CommerceDbContext dbContext) : ICatalogCourseQueries
{
    public async Task<CatalogCoursePage> ListAsync(int page, int size, CancellationToken cancellationToken)
    {
        var query = dbContext.CatalogCourseViews.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var courses = await query.OrderBy(course => course.Title).ThenBy(course => course.CourseId)
            .Skip((page - 1) * size).Take(size)
            .Select(course => new CatalogCourseSummary(course.CourseId, course.Title, course.Level,
                course.InShowcaseSince != null, new CatalogOfferCounts(0, 0, 0)))
            .ToListAsync(cancellationToken);
        return new CatalogCoursePage(courses, new CatalogPagination(page, size, total, (int)Math.Ceiling((double)total / size)));
    }
}
