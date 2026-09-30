using CodeForCoders.Learning.Domain.Entities;

namespace CodeForCoders.Learning.Application.Interfaces;

public interface ICourseVersionStore
{
    void Add(CourseVersion version);
}
