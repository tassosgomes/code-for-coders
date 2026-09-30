using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Learning.UnitTests;

public sealed class CoursePrerequisiteRuleTests
{
    [Fact(DisplayName = nameof(TextAndOrderedIdsAreStoredAndInputCannotMutateTheCourse))]
    public void TextAndOrderedIdsAreStoredAndInputCannotMutateTheCourse()
    {
        var course = Create(); var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() };
        course.Update(Changes(new string('x', 1000), ids));
        Assert.Equal(ids, course.RecommendedCourseIds); Assert.Equal(1000, course.PrerequisiteText!.Length);
        ids[0] = Guid.CreateVersion7(); Assert.NotEqual(ids[0], course.RecommendedCourseIds[0]);
    }

    [Fact(DisplayName = nameof(AbsentFieldsPreserveValuesAndExplicitNullAndEmptyListClearThem))]
    public void AbsentFieldsPreserveValuesAndExplicitNullAndEmptyListClearThem()
    {
        var course = Create(); var id = Guid.CreateVersion7(); course.Update(Changes("Basics", [id]));
        course.Update(new("Renamed", null, false, null, null));
        Assert.Equal("Basics", course.PrerequisiteText); Assert.Equal([id], course.RecommendedCourseIds);
        course.Update(Changes(null, [])); Assert.Null(course.PrerequisiteText); Assert.Empty(course.RecommendedCourseIds);
    }

    [Fact(DisplayName = nameof(InvalidTextRejectsAllChangesWithoutLeakingItsValue))]
    public void InvalidTextRejectsAllChangesWithoutLeakingItsValue()
    {
        var course = Create();
        foreach (var text in new[] { "", new string('x', 1001) })
        {
            var exception = Assert.Throws<CourseRuleException>(() => course.Update(Changes(text, []) with { Title = "Rejected", Level = "advanced", HasLevel = true }));
            Assert.Equal("FIELD_INVALID", exception.Code); Assert.Equal("prerequisiteText", exception.Field);
            Assert.Equal("Course", course.Title); Assert.Null(course.Level); Assert.Null(course.PrerequisiteText);
            if (text.Length > 0) Assert.DoesNotContain(text, exception.Message);
        }
    }

    [Fact(DisplayName = nameof(SelfDuplicateAndMoreThanFiveRecommendationsAreRejected))]
    public void SelfDuplicateAndMoreThanFiveRecommendationsAreRejected()
    {
        var course = Create(); var id = Guid.CreateVersion7();
        Assert.Equal("RECOMMENDED_COURSE_INVALID", Assert.Throws<CourseRuleException>(() => course.Update(Changes("Basics", [course.Id]))).Code);
        Assert.Throws<CourseRuleException>(() => course.Update(Changes("Basics", [id, id])));
        Assert.Throws<CourseRuleException>(() => course.Update(Changes("Basics", Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7()).ToArray())));
        Assert.Null(course.PrerequisiteText); Assert.Empty(course.RecommendedCourseIds);
    }

    [Fact(DisplayName = nameof(TextAndRecommendationOrderAffectFingerprintAndDiscardRestoresLegacyVersion))]
    public void TextAndRecommendationOrderAffectFingerprintAndDiscardRestoresLegacyVersion()
    {
        var course = Create(); var actor = new CourseCreation(course.TenantId, course.CreatedById, "Teacher", course.Title, null, DateTimeOffset.UtcNow);
        var module = course.AddModule(new("Module", null, false, null, null));
        course.AddLesson(module, new("Lesson", null, false, null, null, Guid.CreateVersion7(), true));
        var version = course.Publish(new(1, null, actor));
        course.Update(Changes("Basics", [])); course.RecordEdit(actor); Assert.True(course.HasUnpublishedChanges);
        course.Update(Changes(null, [])); course.RecordEdit(actor); Assert.False(course.HasUnpublishedChanges);
        var ids = new[] { Guid.CreateVersion7(), Guid.CreateVersion7() }; course.Update(Changes(null, ids));
        course.Publish(new(course.DraftRevision, null, actor));
        course.Update(Changes(null, ids.Reverse().ToArray())); course.RecordEdit(actor); Assert.True(course.HasUnpublishedChanges);
        course.Update(Changes(null, ids)); course.RecordEdit(actor); Assert.False(course.HasUnpublishedChanges);
        course.DiscardDraft(course.DraftRevision, version); course.RecordEdit(actor);
        Assert.Null(course.PrerequisiteText); Assert.Empty(course.RecommendedCourseIds); Assert.False(course.HasUnpublishedChanges);
    }

    [Fact(DisplayName = nameof(TitleSearchNormalizesPortugueseAccentsOnCreateRenameAndDiscard))]
    public void TitleSearchNormalizesPortugueseAccentsOnCreateRenameAndDiscard()
    {
        var course = Create(); course.Update(new("AÇÃO À Ê Í Ó Ú Ü", null, false, null, null));
        Assert.Equal("acao a e i o u u", course.TitleSearch);
        var original = Course.Create(new(course.TenantId, course.CreatedById, "Teacher", "Fundaméntos", null, DateTimeOffset.UtcNow));
        Assert.Equal("fundamentos", original.TitleSearch);
    }

    private static Course Create() => Course.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Course", null, DateTimeOffset.UtcNow));
    private static CourseChanges Changes(string? text, IReadOnlyList<Guid> ids) => new(null, null, false, null, null, PrerequisiteText: text, HasPrerequisiteText: true, RecommendedCourseIds: ids);
}
