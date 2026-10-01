namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IShowcaseQueries
{
    Task<ShowcaseCoursePage> ListAsync(string? level, int page, int size, CancellationToken cancellationToken);

    Task<ShowcaseCourseDetail?> GetCourseAsync(Guid courseId, CancellationToken cancellationToken);
}
