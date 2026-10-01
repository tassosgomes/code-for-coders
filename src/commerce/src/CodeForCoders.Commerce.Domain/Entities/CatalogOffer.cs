using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Domain.Entities;

public sealed class CatalogOffer
{
    private CatalogOffer() { }
    public Guid OfferId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Name { get; private set; } = "";
    public int PriceCents { get; private set; }
    public AccessPeriod AccessPeriod { get; private set; } = new("lifetime", null);
    public string Status { get; private set; } = "draft";
    public int OfferRevision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    internal static CatalogOffer Create(CatalogCourseView course, OfferChange change, DateTimeOffset now)
    {
        Validate(change.Name ?? "", change.PriceCents ?? 0, change.AccessPeriod ?? new("", null));
        return new()
        {
            OfferId = Guid.CreateVersion7(),
            TenantId = course.TenantId,
            CourseId = course.CourseId,
            Name = change.Name!,
            PriceCents = (int)change.PriceCents!.Value,
            AccessPeriod = change.AccessPeriod!,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal bool Update(OfferChange change, DateTimeOffset now)
    {
        var name = change.Name ?? Name;
        var price = change.PriceCents ?? PriceCents;
        var period = change.AccessPeriod ?? AccessPeriod;
        Validate(name, price, period);
        if (name == Name && price == PriceCents && period == AccessPeriod) return false;
        Name = name; PriceCents = (int)price; AccessPeriod = period; UpdatedAt = now; OfferRevision++;
        return true;
    }

    internal void Publish(DateTimeOffset now)
    {
        if (Status is not ("draft" or "unpublished"))
            throw new CatalogRuleException("OFFER_STATE_CONFLICT", "The offer is already published.");
        Status = "published";
        PublishedAt = now;
        UpdatedAt = now;
        OfferRevision++;
    }

    internal void Unpublish(DateTimeOffset now)
    {
        if (Status != "published")
            throw new CatalogRuleException("OFFER_STATE_CONFLICT", "Only a published offer can be unpublished.");
        Status = "unpublished";
        UpdatedAt = now;
        OfferRevision++;
    }

    internal void EnsureDeletable()
    {
        if (Status != "draft") throw new CatalogRuleException("OFFER_STATE_CONFLICT", "Only a draft offer can be deleted.");
    }

    private static void Validate(string name, decimal price, AccessPeriod period)
    {
        if (name.Length is < 1 or > 60)
            throw new CatalogRuleException("FIELD_INVALID", "name must contain between 1 and 60 characters.");
        if (price is < 1 or > 9999999 || decimal.Truncate(price) != price)
            throw new CatalogRuleException("FIELD_INVALID", "priceCents must be a whole number between 1 and 9999999.");
        _ = AccessPeriod.Create(period.Type, period.Months);
    }
}
