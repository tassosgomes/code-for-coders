using CodeForCoders.Learning.Application.Exceptions;
using CodeForCoders.Learning.Application.Interfaces;
using CodeForCoders.Learning.Application.UseCases.Courses.Common;
using CodeForCoders.Learning.Domain.SeedWork;
using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.DiscardCourseDraft;

public sealed class DiscardCourseDraft(CourseEditSession session, ICourseVersionStore versions) : IDiscardCourseDraft
{
    public async Task<CourseDetailOutput> ExecuteAsync(DiscardCourseDraftInput input, CancellationToken cancellationToken)
    {
        if (input.DraftRevision < 1) throw new ValidationException("The draft revision must be positive.");
        var result = await session.ExecuteAsync(input.Context, "discard-draft", async (course, token) =>
        {
            if (!course.CurrentVersion.HasValue) throw new CourseRuleException("COURSE_NEVER_PUBLISHED");
            var version = await versions.GetAsync(course.Id, course.CurrentVersion.Value, token)
                ?? throw new NotFoundException("The current course version was not found.");
            course.DiscardDraft(input.DraftRevision, version);
            return null;
        }, cancellationToken);
        return result.Course;
    }
}
