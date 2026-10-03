using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class EntitlementCourseViewTests
{
    [Fact(DisplayName = nameof(FirstPublicationKeepsOnlyTheMinimumCourseView))]
    public void FirstPublicationKeepsOnlyTheMinimumCourseView()
    {
        var snapshot = Snapshot(); var course = EntitlementCourseView.Create(snapshot);
        Assert.Equal(snapshot.TenantId, course.TenantId); Assert.Equal(snapshot.CourseId, course.CourseId);
        Assert.Equal("Ação", course.Title); Assert.Equal("acao", course.NormalizedTitle); Assert.Equal(2, course.VersionNumber);
        Assert.Equal(5, typeof(EntitlementCourseView).GetProperties().Length);
    }

    [Theory(DisplayName = nameof(EqualAndOlderVersionsDoNotChangeTitleEvenWithRicherFormat))]
    [InlineData(1)]
    [InlineData(2)]
    public void EqualAndOlderVersionsDoNotChangeTitleEvenWithRicherFormat(int version)
    {
        var snapshot = Snapshot(); var course = EntitlementCourseView.Create(snapshot);
        Assert.False(course.Apply(snapshot with { VersionNumber = version, Title = "Ignored", SourceFormat = "1.1.0" }));
        Assert.Equal("Ação", course.Title); Assert.Equal("acao", course.NormalizedTitle); Assert.Equal(2, course.VersionNumber);
    }

    [Fact(DisplayName = nameof(NewerPublicationUpdatesTitleAndSearchText))]
    public void NewerPublicationUpdatesTitleAndSearchText()
    {
        var snapshot = Snapshot(); var course = EntitlementCourseView.Create(snapshot);
        Assert.True(course.Apply(snapshot with { VersionNumber = 3, Title = "Fundaméntos" }));
        Assert.Equal("Fundaméntos", course.Title); Assert.Equal("fundamentos", course.NormalizedTitle); Assert.Equal(3, course.VersionNumber);
    }

    [Fact(DisplayName = nameof(AnotherSchoolOrCourseCannotChangeTheView))]
    public void AnotherSchoolOrCourseCannotChangeTheView()
    {
        var snapshot = Snapshot(); var course = EntitlementCourseView.Create(snapshot);
        Assert.Throws<EntityValidationException>(() => course.Apply(snapshot with { TenantId = Guid.CreateVersion7(), VersionNumber = 3 }));
        Assert.Throws<EntityValidationException>(() => course.Apply(snapshot with { CourseId = Guid.CreateVersion7(), VersionNumber = 3 }));
        Assert.Equal(2, course.VersionNumber);
    }

    [Fact(DisplayName = nameof(InvalidPublicationCannotCreateAView))]
    public void InvalidPublicationCannotCreateAView()
    {
        var snapshot = Snapshot();
        Assert.Throws<EntityValidationException>(() => EntitlementCourseView.Create(snapshot with { VersionNumber = 0 }));
        Assert.Throws<EntityValidationException>(() => EntitlementCourseView.Create(snapshot with { TenantId = Guid.Empty }));
        Assert.Throws<EntityValidationException>(() => EntitlementCourseView.Create(snapshot with { Title = " " }));
    }

    private static PublishedCourseSnapshot Snapshot() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), 2,
        "1.0.0", "Ação", "", null, "{}", "[]", DateTimeOffset.UtcNow);
}
