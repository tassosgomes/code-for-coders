namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IExistingCourseAccessQueries
{
    Task<ExistingCourseAccess?> FindAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken);
}
