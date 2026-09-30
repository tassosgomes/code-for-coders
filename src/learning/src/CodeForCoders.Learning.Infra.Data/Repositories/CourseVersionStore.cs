using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Infra.Data.Repositories;

public sealed class CourseVersionStore(LearningDbContext context) : ICourseVersionStore
{
    public void Add(CourseVersion version) => context.CourseVersions.Add(version);
}
