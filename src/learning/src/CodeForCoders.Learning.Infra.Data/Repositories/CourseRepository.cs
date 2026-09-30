using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Learning.Infra.Data.Repositories;

public sealed class CourseRepository(LearningDbContext dbContext) : ICourseRepository
{
    public async Task<Course?> GetAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var course = await dbContext.Courses.Include(course => course.Modules).ThenInclude(module => module.Lessons)
            .SingleOrDefaultAsync(course => course.Id == courseId, cancellationToken);
        if (course is not null)
        {
            course.Modules.Sort((left, right) => left.Position.CompareTo(right.Position));
            foreach (var module in course.Modules) module.Lessons.Sort((left, right) => left.Position.CompareTo(right.Position));
        }
        return course;
    }

    public Task AddAsync(Course course, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dbContext.Courses.Add(course);
        return Task.CompletedTask;
    }
}
