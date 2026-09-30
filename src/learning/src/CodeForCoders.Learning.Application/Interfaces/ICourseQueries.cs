namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseQueries
{
    Task<IReadOnlyList<Guid>> PublishedIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<CourseReference>> ResolveAsync(Guid[] ids, CancellationToken cancellationToken);

    Task<CoursePage> ListAsync(CourseListQuery query, CancellationToken cancellationToken);
}
