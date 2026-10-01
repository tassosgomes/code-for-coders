using CodeForCoders.Commerce.Domain.Entities;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class CatalogTaglineTests
{
    private static CatalogCourseView CreateCourse() => CatalogCourseView.Create(new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "1.1.0", "Course", "Description", "beginner",
        "{\"text\":null,\"recommendedCourses\":[]}", "[]", DateTimeOffset.UtcNow));

    [Theory(DisplayName = nameof(TaglineAcceptsBothInclusiveLimits))]
    [InlineData(1)]
    [InlineData(160)]
    public void TaglineAcceptsBothInclusiveLimits(int length)
    {
        var course = CreateCourse(); var tagline = new string('a', length);
        course.UpdateTagline(tagline); Assert.Equal(tagline, course.Tagline);
    }

    [Theory(DisplayName = nameof(InvalidTaglineNamesTheFieldAndLimitWithoutChangingIt))]
    [InlineData(0)]
    [InlineData(161)]
    public void InvalidTaglineNamesTheFieldAndLimitWithoutChangingIt(int length)
    {
        var course = CreateCourse(); course.UpdateTagline("Original");
        var error = Assert.Throws<CatalogRuleException>(() => course.UpdateTagline(new string('a', length)));
        Assert.Equal("FIELD_INVALID", error.Code); Assert.Contains("tagline", error.Message); Assert.Contains("160", error.Message);
        Assert.Equal("Original", course.Tagline);
    }

    [Fact(DisplayName = nameof(NullClearsTaglineAndPublicationDoesNotOverwriteIt))]
    public void NullClearsTaglineAndPublicationDoesNotOverwriteIt()
    {
        var course = CreateCourse(); course.UpdateTagline("Commercial");
        course.Apply(new(course.TenantId, course.CourseId, 2, "1.1.0", "New title", "Description", "advanced", "{}", "[]", DateTimeOffset.UtcNow));
        Assert.Equal("Commercial", course.Tagline);
        course.UpdateTagline(null); Assert.Null(course.Tagline);
    }
}
