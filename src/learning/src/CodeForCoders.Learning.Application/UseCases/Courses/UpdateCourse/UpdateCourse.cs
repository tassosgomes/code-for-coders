using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.SeedWork;

namespace CodeForCoders.Learning.Application.UseCases.Courses.UpdateCourse;

public sealed class UpdateCourse(CourseEditSession session, ICourseQueries queries) : IUpdateCourse
{
    public Task<CourseEditOutput> ExecuteAsync(UpdateCourseInput input, CancellationToken cancellationToken)
        => session.ExecuteAsync(input.Context, "update-course", async (course, token) =>
        {
            if (input.Changes.RecommendedCourseIds is { Count: > 0 } recommended)
            {
                var published = await queries.PublishedIdsAsync(recommended, token);
                for (var index = 0; index < recommended.Count; index++)
                    if (recommended[index] == course.Id || !published.Contains(recommended[index]))
                        throw new CourseRuleException("RECOMMENDED_COURSE_INVALID", $"recommendedCourseIds[{index}]");
            }
            course.Update(input.Changes);
            return null;
        }, cancellationToken);
}
