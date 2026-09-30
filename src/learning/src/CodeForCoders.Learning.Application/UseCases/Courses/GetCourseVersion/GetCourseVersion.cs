using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.Repositories;
using CodeForCoders.Learning.Domain.SeedWork;

namespace CodeForCoders.Learning.Application.UseCases.Courses.GetCourseVersion;

public sealed class GetCourseVersion(ICourseRepository courses, ICourseVersionStore versions) : IGetCourseVersion
{
    public async Task<CourseVersionOutput> ExecuteAsync(GetCourseVersionInput input, CancellationToken cancellationToken)
    {
        var course = await courses.GetAsync(input.CourseId, cancellationToken)
            ?? throw new NotFoundException("The requested course was not found.");
        var version = await versions.GetAsync(input.CourseId, input.VersionNumber, cancellationToken)
            ?? throw new CourseItemNotFoundException("VERSION_NOT_FOUND");
        return CourseVersionOutput.FromVersion(version, course.CurrentVersion == version.VersionNumber);
    }
}
