using CodeForCoders.Commerce.Domain.Entities;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class CatalogCourseViewTests
{
    private static PublishedCourseSnapshot Snapshot() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), 1,
        "1.0.0", "Published course", "", null, "{}", "[]", DateTimeOffset.UtcNow);

    [Fact(DisplayName = nameof(DuplicateAndOlderFactsLeaveTheViewUnchanged))]
    public void DuplicateAndOlderFactsLeaveTheViewUnchanged()
    {
        var snapshot = Snapshot() with { VersionNumber = 2 };
        var course = CatalogCourseView.Create(snapshot);
        Assert.False(course.Apply(snapshot with { Title = "Duplicate" }));
        Assert.False(course.Apply(snapshot with { VersionNumber = 1, Title = "Older" }));
        Assert.Equal("Published course", course.Title);
    }

    [Fact(DisplayName = nameof(RicherFormatUpgradesTheSameVersionOnlyOnce))]
    public void RicherFormatUpgradesTheSameVersionOnlyOnce()
    {
        var snapshot = Snapshot(); var course = CatalogCourseView.Create(snapshot);
        var rich = snapshot with { SourceFormat = "1.1.0", Level = "beginner", Description = "Description" };
        Assert.True(course.Apply(rich));
        Assert.Equal("beginner", course.Level); Assert.Equal("Description", course.Description);
        Assert.False(course.Apply(rich with { Description = "Duplicate" }));
        Assert.False(course.Apply(snapshot));
        Assert.Equal("1.1.0", course.SourceFormat);
    }

    [Fact(DisplayName = nameof(NewerPublicationReplacesAllDerivedFields))]
    public void NewerPublicationReplacesAllDerivedFields()
    {
        var snapshot = Snapshot() with { SourceFormat = "1.1.0", Level = "advanced", Description = "Old", PrerequisiteJson = "{\"text\":\"Old\"}" };
        var course = CatalogCourseView.Create(snapshot);
        Assert.True(course.Apply(snapshot with { VersionNumber = 2, Title = "New", Description = "", Level = null, PrerequisiteJson = "{}", StructureJson = "[]" }));
        Assert.Equal("New", course.Title); Assert.Null(course.Level); Assert.Equal("", course.Description); Assert.Equal("{}", course.PrerequisiteJson);
    }

    [Theory(DisplayName = nameof(ShowcaseRequiresLevelAndPublishedOffer))]
    [InlineData(null, 1, false)]
    [InlineData("beginner", 0, false)]
    [InlineData("advanced", 1, true)]
    public void ShowcaseRequiresLevelAndPublishedOffer(string? level, int offers, bool expected)
        => Assert.Equal(expected, CatalogCourseView.IsShowcaseEligible(level, offers));

    [Fact(DisplayName = nameof(ShowcaseTimeIsPreservedUntilEligibilityIsLost))]
    public void ShowcaseTimeIsPreservedUntilEligibilityIsLost()
    {
        var snapshot = Snapshot() with { SourceFormat = "1.1.0", Level = "beginner" };
        var course = CatalogCourseView.Create(snapshot); var now = DateTimeOffset.UtcNow;
        course.RefreshShowcase(1, now); course.RefreshShowcase(2, now.AddMinutes(1));
        Assert.Equal(now, course.InShowcaseSince);
        course.Apply(snapshot with { VersionNumber = 2, Level = null }); Assert.Null(course.InShowcaseSince);
        course.Apply(snapshot with { VersionNumber = 3 }); course.RefreshShowcase(1, now.AddMinutes(2));
        Assert.Equal(now.AddMinutes(2), course.InShowcaseSince);
        course.RefreshShowcase(0, now.AddMinutes(3)); Assert.False(course.InShowcase);
    }
}
