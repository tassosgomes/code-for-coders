using CodeForCoders.Commerce.Domain.SeedWork;

namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class EntitlementCourseView
{
    private EntitlementCourseView() { }
    public Guid TenantId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = "";
    public string NormalizedTitle { get; private set; } = "";
    public int VersionNumber { get; private set; }

    public static EntitlementCourseView Create(PublishedCourseSnapshot snapshot)
    {
        var course = new EntitlementCourseView { TenantId = snapshot.TenantId, CourseId = snapshot.CourseId };
        course.Apply(snapshot);
        return course;
    }

    public bool Apply(PublishedCourseSnapshot snapshot)
    {
        if (snapshot.TenantId == Guid.Empty || snapshot.CourseId == Guid.Empty
            || snapshot.TenantId != TenantId || snapshot.CourseId != CourseId
            || snapshot.VersionNumber < 1 || string.IsNullOrWhiteSpace(snapshot.Title) || snapshot.Title.Length > 200)
            throw new EntityValidationException("The course publication is invalid.");
        if (snapshot.VersionNumber <= VersionNumber) return false;
        Title = snapshot.Title;
        NormalizedTitle = CourtesyTitleSearch.Normalize(snapshot.Title);
        VersionNumber = snapshot.VersionNumber;
        return true;
    }
}
