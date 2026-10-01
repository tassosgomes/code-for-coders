using CodeForCoders.Commerce.Domain.SeedWork;

namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class CatalogCourseView
{
    private CatalogCourseView() { }

    public Guid TenantId { get; private set; }
    public Guid CourseId { get; private set; }
    public int VersionNumber { get; private set; }
    public string SourceFormat { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string? Level { get; private set; }
    public string? Tagline { get; private set; }
    public string PrerequisiteJson { get; private set; } = "{}";
    public string StructureJson { get; private set; } = "[]";
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? InShowcaseSince { get; private set; }
    public bool InShowcase => InShowcaseSince.HasValue;

    public static CatalogCourseView Create(PublishedCourseSnapshot snapshot)
    {
        var course = new CatalogCourseView { TenantId = snapshot.TenantId, CourseId = snapshot.CourseId };
        course.Apply(snapshot);
        return course;
    }

    public bool Apply(PublishedCourseSnapshot snapshot)
    {
        if (snapshot.TenantId != TenantId || snapshot.CourseId != CourseId)
            throw new EntityValidationException("The publication belongs to another course.");
        if (snapshot.VersionNumber < 1 || snapshot.SourceFormat is not ("1.0.0" or "1.1.0"))
            throw new EntityValidationException("The publication version or format is invalid.");
        if (snapshot.VersionNumber < VersionNumber || snapshot.VersionNumber == VersionNumber
            && !(SourceFormat == "1.0.0" && snapshot.SourceFormat == "1.1.0")) return false;

        VersionNumber = snapshot.VersionNumber;
        SourceFormat = snapshot.SourceFormat;
        Title = snapshot.Title;
        Description = snapshot.Description;
        Level = snapshot.Level;
        PrerequisiteJson = snapshot.PrerequisiteJson;
        StructureJson = snapshot.StructureJson;
        PublishedAt = snapshot.PublishedAt;
        if (Level is null) InShowcaseSince = null;
        return true;
    }

    public static bool IsShowcaseEligible(string? level, int publishedOfferCount)
        => level is not null && publishedOfferCount > 0;

    public void UpdateTagline(string? tagline)
    {
        if (tagline is not null && tagline.Length is < 1 or > 160)
            throw new CatalogRuleException("FIELD_INVALID", "tagline must contain between 1 and 160 characters.");
        Tagline = tagline;
    }

    public void RefreshShowcase(int publishedOfferCount, DateTimeOffset now)
    {
        InShowcaseSince = IsShowcaseEligible(Level, publishedOfferCount) ? InShowcaseSince ?? now : null;
    }
}
