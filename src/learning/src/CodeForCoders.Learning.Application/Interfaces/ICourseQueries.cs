namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseQueries
{
    Task<CoursePage> ListAsync(CourseListQuery query, CancellationToken cancellationToken);
}
