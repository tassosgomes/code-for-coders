using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Domain.Repositories;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.ListCourseVersions;

public sealed class ListCourseVersions(ICourseRepository courses, ICourseVersionStore versions) : IListCourseVersions
{
    public async Task<CourseVersionSummaryPage> ExecuteAsync(ListCourseVersionsInput input, CancellationToken cancellationToken)
    {
        if (input.Page < 1 || input.Size is < 1 or > 50 || (long)(input.Page - 1) * input.Size > int.MaxValue)
            throw new ValidationException("The requested page is invalid.");
        var course = await courses.GetAsync(input.CourseId, cancellationToken)
            ?? throw new NotFoundException("The requested course was not found.");
        return await versions.ListAsync(input.CourseId, course.CurrentVersion ?? 0, input.Page, input.Size, cancellationToken);
    }
}
