using CodeForCoders.Learning.Domain.Entities;
using CodeForCoders.Learning.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Learning.UnitTests;

public sealed class CourseCreationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTitleCannotBecomeADraft(string title)
    {
        var input = new CourseCreation(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", title, null, DateTimeOffset.UtcNow);
        var error = Assert.Throws<CourseRuleException>(() => Course.Create(input));
        Assert.Equal("TITLE_REQUIRED", error.Code);
    }

    [Fact]
    public void NewDraftPreservesItsSchoolAndCreatorAndNormalizesTheTitle()
    {
        var input = new CourseCreation(Guid.CreateVersion7(), Guid.CreateVersion7(), "Teacher", "  API  ", "Description", DateTimeOffset.UtcNow);
        var course = Course.Create(input);
        Assert.Equal(input.TenantId, course.TenantId);
        Assert.Equal(input.ActorId, course.CreatedById);
        Assert.Equal(input.ActorId, course.LastEditedById);
        Assert.Equal("API", course.Title);
        Assert.Equal(input.Description, course.Description);
        Assert.Equal(1, course.DraftRevision);
        Assert.Null(course.CurrentVersion);
    }
}
