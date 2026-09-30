using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Repositories;

public sealed class CourseRepository(LearningDbContext dbContext) : ICourseRepository
{
    public Task<Course?> GetAsync(Guid courseId, CancellationToken cancellationToken)
        => dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(course => course.Id == courseId, cancellationToken);

    public Task AddAsync(Course course, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Courses.Add(course);
        return Task.CompletedTask;
    }
}
