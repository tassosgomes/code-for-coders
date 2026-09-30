using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseCreationStore
{
    Task<ICourseCreationTransaction> LockAsync(CourseCreationScope scope, CancellationToken cancellationToken);
    Task<CourseCreationReceipt?> FindAsync(CourseCreationScope scope, CancellationToken cancellationToken);
    void Add(CourseCreationReceipt receipt);
}
