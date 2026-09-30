using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseEditStore
{
    Task<ICourseCreationTransaction> LockAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CourseEditReceipt?> FindAsync(CourseEditScope scope, CancellationToken cancellationToken);
    void Add(CourseEditReceipt receipt);
}
