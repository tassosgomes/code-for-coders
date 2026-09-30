using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Learning.UnitTests;

public sealed class CourseDeletionRuleTests
{
    [Fact(DisplayName = nameof(NeverPublishedDraftCanBeDeleted))]
    public void NeverPublishedDraftCanBeDeleted()
    {
        var course = Create(); course.EnsureNeverPublished(); Assert.Null(course.CurrentVersion);
    }

    [Fact(DisplayName = nameof(PublishedCourseCannotBeDeletedAfterDraftEdits))]
    public void PublishedCourseCannotBeDeletedAfterDraftEdits()
    {
        var course = Create();
        var module = course.AddModule(new("Module", null, false, null, null, null, false));
        course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
        course.Publish(new(1, null, new(course.TenantId, Guid.CreateVersion7(), "Teacher", course.Title, null, DateTimeOffset.UtcNow)));
        course.Update(new("Changed draft", null, false, null, null, null, false));
        Assert.Equal("COURSE_ALREADY_PUBLISHED", Assert.Throws<CourseRuleException>(course.EnsureNeverPublished).Code);
        Assert.Equal(1, course.CurrentVersion); Assert.Equal("Changed draft", course.Title);
    }

    private static Course Create() => Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Draft", null, DateTimeOffset.UtcNow));
}
