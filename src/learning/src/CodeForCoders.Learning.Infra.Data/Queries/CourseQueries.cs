using CodeForCoders.Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Queries;

public sealed class CourseQueries(LearningDbContext dbContext) : ICourseQueries
{
    public async Task<IReadOnlyList<CourseReference>> ResolveAsync(Guid[] ids, CancellationToken cancellationToken)
    {
        var titles = await dbContext.Courses.AsNoTracking().Where(course => ids.Contains(course.Id))
            .ToDictionaryAsync(course => course.Id, course => course.Title, cancellationToken);
        return ids.Distinct().Select(id => new CourseReference(id, titles.GetValueOrDefault(id))).ToArray();
    }

    public async Task<CoursePage> ListAsync(CourseListQuery query, CancellationToken cancellationToken)
    {
        var courses = dbContext.Courses.AsNoTracking();
        if (query.Status is not null)
            courses = courses.Where(course => (course.CurrentVersion != null) == (query.Status == "published"));
        var total = await courses.LongCountAsync(cancellationToken);
        var data = await courses.OrderByDescending(course => course.LastEditedAt).ThenByDescending(course => course.Id)
            .Skip((query.Page - 1) * query.Size).Take(query.Size)
            .Select(course => new CourseSummary(course.Id, course.Title, course.CurrentVersion.HasValue ? "published" : "draft",
                course.CurrentVersion, course.HasUnpublishedChanges, course.LastEditedAt, new CourseActor(course.LastEditedByName), course.CurrentLevel))
            .ToListAsync(cancellationToken);
        return new CoursePage(data, new CoursePagination(query.Page, query.Size, total, (total + query.Size - 1) / query.Size));
    }
}
