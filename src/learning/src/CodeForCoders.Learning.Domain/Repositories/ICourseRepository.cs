using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Domain.Repositories;

public interface ICourseRepository
{
    Task<Course?> GetAsync(Guid courseId, CancellationToken cancellationToken);
    Task AddAsync(Course course, CancellationToken cancellationToken);
    void Remove(Course course);
}
