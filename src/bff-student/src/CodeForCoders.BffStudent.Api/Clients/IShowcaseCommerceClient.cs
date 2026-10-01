using CodeForCoders.BffStudent.Contracts;

namespace CodeForCoders.BffStudent.Api.Clients;

public interface IShowcaseCommerceClient
{
    Task<ShowcaseResult<ShowcaseCoursePageV1>> ListCoursesAsync(string? level, int page, int size, CancellationToken cancellationToken);

    Task<ShowcaseResult<ShowcaseCourseDetailV1>> GetCourseAsync(Guid courseId, CancellationToken cancellationToken);
}
