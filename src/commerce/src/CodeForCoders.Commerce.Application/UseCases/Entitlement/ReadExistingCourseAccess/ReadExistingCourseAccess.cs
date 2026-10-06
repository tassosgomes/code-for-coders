using CodeForCoders.Commerce.Application.Interfaces;
namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.ReadExistingCourseAccess;

public sealed class ReadExistingCourseAccess(IExistingCourseAccessQueries queries) : IReadExistingCourseAccess, IExistingCourseAccessReader
{
    public Task<ExistingCourseAccess?> ExecuteAsync(ExistingCourseAccessInput input, CancellationToken cancellationToken) => queries.FindAsync(input.StudentId, input.CourseId, cancellationToken);
    public Task<ExistingCourseAccess?> FindAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken) => ExecuteAsync(new(studentId, courseId), cancellationToken);
}
