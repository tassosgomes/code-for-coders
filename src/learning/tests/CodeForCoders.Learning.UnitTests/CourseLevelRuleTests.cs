using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Learning.UnitTests;

public sealed class CourseLevelRuleTests
{
    [Fact(DisplayName = nameof(AcceptsAllDeclaredLevels))]
    public void AcceptsAllDeclaredLevels()
    {
        var course = Create();
        foreach (var level in new[] { "beginner", "intermediate", "advanced" })
        {
            course.Update(Changes(level));
            Assert.Equal(level, course.Level);
        }
    }

    [Fact(DisplayName = nameof(AbsentLevelPreservesValueAndExplicitNullClearsIt))]
    public void AbsentLevelPreservesValueAndExplicitNullClearsIt()
    {
        var course = Create(); course.Update(Changes("beginner"));
        course.Update(new("Renamed", null, false, null, null));
        Assert.Equal("beginner", course.Level);
        course.Update(Changes(null)); Assert.Null(course.Level);
    }

    [Fact(DisplayName = nameof(InvalidLevelRejectsEntireEdit))]
    public void InvalidLevelRejectsEntireEdit()
    {
        var course = Create();
        foreach (var level in new[] { "expert", "Beginner", "", " beginner " })
        {
            var failure = Assert.Throws<CourseRuleException>(() => course.Update(Changes(level) with { Title = "Rejected" }));
            Assert.Equal("FIELD_INVALID", failure.Code); Assert.Equal("level", failure.Field);
            Assert.Equal("Course", course.Title); Assert.Null(course.Level);
        }
    }

    [Fact(DisplayName = nameof(LevelOnlyEditChangesFingerprintAndRevertOrDiscardRestoresIt))]
    public void LevelOnlyEditChangesFingerprintAndRevertOrDiscardRestoresIt()
    {
        var course = Create(); var actor = new CourseCreation(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow);
        var module = course.AddModule(new("Module", null, false, null, null));
        course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
        var version = course.Publish(new(course.DraftRevision, null, actor));
        course.Update(Changes("beginner")); course.RecordEdit(actor);
        Assert.True(course.HasUnpublishedChanges); Assert.Equal(2, course.DraftRevision); Assert.Null(course.CurrentLevel);
        course.Update(Changes(null)); course.RecordEdit(actor); Assert.False(course.HasUnpublishedChanges);
        course.Update(Changes("advanced")); course.RecordEdit(actor);
        course.DiscardDraft(course.DraftRevision, version); course.RecordEdit(actor);
        Assert.Null(course.Level); Assert.False(course.HasUnpublishedChanges); Assert.Equal(1, course.CurrentVersion);
    }

    private static Course Create() => Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Course", null, DateTimeOffset.UtcNow));
    private static CourseChanges Changes(string? level) => new(null, null, false, null, null, Level: level, HasLevel: true);
}
