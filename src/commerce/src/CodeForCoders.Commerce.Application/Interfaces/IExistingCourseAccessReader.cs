namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IExistingCourseAccessReader
{
    Task<ExistingCourseAccess?> FindAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken);
}
