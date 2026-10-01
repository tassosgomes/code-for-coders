using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class OfferPublicationRulesTests
{
    private static CatalogCourseView Course(string? level = "beginner") => CatalogCourseView.Create(new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "1.1.0", "Course", "Description", level, "{}", "[]", DateTimeOffset.UtcNow));
    private static CatalogOffer Offer(CatalogCourseView course) => course.CreateOffer(new("Option", 49700, AccessPeriod.Create("months", 12)), DateTimeOffset.UtcNow);

    [Fact(DisplayName = nameof(PublishIncrementsRevisionAndEmitsCurrentTerms))]
    public void PublishIncrementsRevisionAndEmitsCurrentTerms()
    {
        var course = Course(); var offer = Offer(course); var now = DateTimeOffset.UtcNow;
        var fact = course.PublishOffer(offer.OfferId, now);
        Assert.Equal("published", offer.Status); Assert.Equal(now, offer.PublishedAt);
        Assert.Equal(2, fact.OfferRevision); Assert.Equal(offer.OfferId, fact.OfferId); Assert.Equal(course.TenantId, fact.TenantId);
        Assert.Equal(offer.Name, fact.Name); Assert.Equal(offer.PriceCents, fact.PriceCents); Assert.Equal(offer.AccessPeriod, fact.AccessPeriod);
        Assert.Equal("BRL", fact.Currency); Assert.Equal(now, course.InShowcaseSince);
    }

    [Fact(DisplayName = nameof(MissingLevelLeavesDraftAndShowcaseUnchanged))]
    public void MissingLevelLeavesDraftAndShowcaseUnchanged()
    {
        var course = Course(null); var offer = Offer(course);
        var error = Assert.Throws<CatalogRuleException>(() => course.PublishOffer(offer.OfferId, DateTimeOffset.UtcNow));
        Assert.Equal("COURSE_LEVEL_REQUIRED", error.Code); Assert.Equal("draft", offer.Status);
        Assert.Equal(1, offer.OfferRevision); Assert.Null(offer.PublishedAt); Assert.False(course.InShowcase);
    }

    [Fact(DisplayName = nameof(AlreadyPublishedConflictsWithoutAnotherRevision))]
    public void AlreadyPublishedConflictsWithoutAnotherRevision()
    {
        var course = Course(); var offer = Offer(course); var now = DateTimeOffset.UtcNow; course.PublishOffer(offer.OfferId, now);
        var error = Assert.Throws<CatalogRuleException>(() => course.PublishOffer(offer.OfferId, now.AddMinutes(1)));
        Assert.Equal("OFFER_STATE_CONFLICT", error.Code); Assert.Equal(2, offer.OfferRevision); Assert.Equal(now, offer.PublishedAt);
    }

    [Fact(DisplayName = nameof(RepublicationProducesANewFactAndCurrentTerms))]
    public void RepublicationProducesANewFactAndCurrentTerms()
    {
        var course = Course(); var offer = Offer(course); var first = course.PublishOffer(offer.OfferId, DateTimeOffset.UtcNow);
        // The unpublication use case is delivered in 8.0; emulate its persisted state.
        typeof(CatalogOffer).GetProperty(nameof(CatalogOffer.Status))!.SetValue(offer, "unpublished");
        course.UpdateOffer(offer.OfferId, new(null, 89700, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow.AddMinutes(1); var second = course.PublishOffer(offer.OfferId, now);
        Assert.NotEqual(first.EventId, second.EventId); Assert.Equal(4, second.OfferRevision); Assert.Equal(89700, second.PriceCents);
        Assert.Equal("lifetime", second.AccessPeriod.Type); Assert.Equal(now, offer.PublishedAt);
    }

    [Fact(DisplayName = nameof(SecondPublishedOfferPreservesShowcaseEntryTime))]
    public void SecondPublishedOfferPreservesShowcaseEntryTime()
    {
        var course = Course(); var first = Offer(course); var second = Offer(course); var now = DateTimeOffset.UtcNow;
        course.PublishOffer(first.OfferId, now); course.PublishOffer(second.OfferId, now.AddMinutes(1));
        Assert.Equal(now, course.InShowcaseSince); Assert.Equal(2, course.Offers.Count(offer => offer.Status == "published"));
    }
}
