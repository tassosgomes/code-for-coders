using CodeForCoders.Commerce.Domain.SeedWork;
using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class CatalogCourseView
{
    private CatalogCourseView() { }
    private readonly List<CatalogOffer> _offers = [];
    public IReadOnlyCollection<CatalogOffer> Offers => _offers.AsReadOnly();

    public CatalogOffer CreateOffer(OfferChange change, DateTimeOffset now)
    {
        if (_offers.Count >= 50) throw new CatalogRuleException("FIELD_INVALID", "offers cannot exceed 50 per course.");
        var offer = CatalogOffer.Create(this, change, now);
        _offers.Add(offer);
        return offer;
    }

    public Events.OfferChanged? UpdateOffer(Guid offerId, OfferChange change, DateTimeOffset now)
    {
        var offer = _offers.Single(item => item.OfferId == offerId);
        var previous = new Events.OfferPreviousTerms(offer.PriceCents, offer.AccessPeriod);
        if (!offer.Update(change, now) || offer.Status != "published"
            || previous.PriceCents == offer.PriceCents && previous.AccessPeriod == offer.AccessPeriod) return null;
        return new(Guid.CreateVersion7(), TenantId, offerId, CourseId, offer.OfferRevision, now,
            offer.Name, offer.PriceCents, "BRL", offer.AccessPeriod, previous);
    }

    public Events.OfferPublished PublishOffer(Guid offerId, DateTimeOffset now)
    {
        if (Level is null)
            throw new CatalogRuleException("COURSE_LEVEL_REQUIRED", "The current course version needs a level before publication.");
        var offer = _offers.Single(item => item.OfferId == offerId);
        offer.Publish(now);
        RefreshShowcase(_offers.Count(item => item.Status == "published"), now);
        return new(Guid.CreateVersion7(), TenantId, offerId, CourseId, offer.OfferRevision, now,
            offer.Name, offer.PriceCents, "BRL", offer.AccessPeriod);
    }

    public Events.OfferUnpublished UnpublishOffer(Guid offerId, DateTimeOffset now)
    {
        var offer = _offers.Single(item => item.OfferId == offerId);
        offer.Unpublish(now);
        RefreshShowcase(_offers.Count(item => item.Status == "published"), now);
        return new(Guid.CreateVersion7(), TenantId, offerId, CourseId, offer.OfferRevision, now,
            offer.Name, offer.PriceCents, "BRL", offer.AccessPeriod);
    }

    public void DeleteOffer(Guid offerId)
    {
        var offer = _offers.Single(item => item.OfferId == offerId);
        offer.EnsureDeletable();
        _offers.Remove(offer);
    }

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
