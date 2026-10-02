using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Queries;

public sealed class CourtesyCourseQueries(CommerceDbContext dbContext) : ICourtesyCourseQueries
{
    public async Task<CourtesyCoursePage> ListAsync(CourtesyCoursesQuery input, CancellationToken cancellationToken)
    {
        var query = dbContext.EntitlementCourseViews.AsNoTracking();
        if (input.Title is not null)
        {
            var title = CourtesyTitleSearch.Normalize(input.Title);
            query = query.Where(course => course.NormalizedTitle.Contains(title));
        }
        var total = await query.CountAsync(cancellationToken);
        var courses = await query.OrderBy(course => course.Title).ThenBy(course => course.CourseId)
            .Skip((input.Page - 1) * input.Size).Take(input.Size)
            .Select(course => new CourtesyCourse(course.CourseId, course.Title)).ToListAsync(cancellationToken);
        return new(courses, new(input.Page, input.Size, total, (int)Math.Ceiling((double)total / input.Size)));
    }
}
