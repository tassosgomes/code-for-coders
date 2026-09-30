using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Learning.UnitTests;

public sealed class CourseStructureRuleTests
{
    private static Course Course() => CodeForCoders.Learning.Domain.Entities.Course.Create(new CourseCreation(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "Title", null, DateTimeOffset.UtcNow));
    private static CourseChanges Changes(string? title = null, int? position = null, Guid? moduleId = null) => new(title, null, false, position, moduleId);

    [Fact(DisplayName = nameof(InvalidReorderDoesNotApplyRename))]
    public void InvalidReorderDoesNotApplyRename()
    {
        var course = Course(); var module = course.AddModule(Changes("Original"));
        Assert.Throws<CourseRuleException>(() => course.UpdateModule(module, Changes("Rename", 2)));
        Assert.Equal("Original", course.Modules[0].Title); Assert.Equal(1, course.Modules[0].Position);
    }

    [Fact(DisplayName = nameof(InvalidDestinationDoesNotApplyLessonChanges))]
    public void InvalidDestinationDoesNotApplyLessonChanges()
    {
        var course = Course(); var module = course.AddModule(Changes("Module"));
        var lesson = course.AddLesson(module, Changes("Original"));
        Assert.Throws<CourseItemNotFoundException>(() => course.UpdateLesson(lesson, Changes("Rename", moduleId: Guid.CreateVersion7())));
        Assert.Equal("Original", course.Modules[0].Lessons[0].Title); Assert.Equal(module, course.Modules[0].Lessons[0].ModuleId);
    }

    [Fact(DisplayName = nameof(MovementAtDestinationLimitIsAtomic))]
    public void MovementAtDestinationLimitIsAtomic()
    {
        var course = Course(); var source = course.AddModule(Changes("Source")); var destination = course.AddModule(Changes("Full"));
        var lesson = course.AddLesson(source, Changes("Keep"));
        for (var index = 0; index < 200; index++) course.AddLesson(destination, Changes($"Lesson {index}"));
        Assert.Throws<CourseRuleException>(() => course.UpdateLesson(lesson, Changes("Rename", moduleId: destination)));
        Assert.Equal(200, course.Modules[1].Lessons.Count); Assert.Equal("Keep", Assert.Single(course.Modules[0].Lessons).Title);
    }

    [Fact(DisplayName = nameof(RemovedModuleRecreationCannotReuseIdentities))]
    public void RemovedModuleRecreationCannotReuseIdentities()
    {
        var course = Course(); var module = course.AddModule(Changes("Same")); var lesson = course.AddLesson(module, Changes("Same"));
        course.RemoveModule(module); var replacement = course.AddModule(Changes("Same")); var replacementLesson = course.AddLesson(replacement, Changes("Same"));
        Assert.NotEqual(module, replacement); Assert.NotEqual(lesson, replacementLesson);
        Assert.Equal(1, course.Modules[0].Position); Assert.Equal(1, course.Modules[0].Lessons[0].Position);
    }
}
