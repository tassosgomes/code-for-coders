using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class OfferUnpublishRulesTests
{
    private static CatalogCourseView Course() => CatalogCourseView.Create(new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "1.1.0", "Course", "Description", "beginner", "{}", "[]", DateTimeOffset.UtcNow));
    private static CatalogOffer Published(CatalogCourseView course, DateTimeOffset now)
    {
        var offer = course.CreateOffer(new("Option", 49700, AccessPeriod.Create("months", 12)), now);
        course.PublishOffer(offer.OfferId, now);
        return offer;
    }

    [Fact(DisplayName = nameof(UnpublishIncrementsRevisionKeepsTermsAndPublicationMoment))]
    public void UnpublishIncrementsRevisionKeepsTermsAndPublicationMoment()
    {
        var course = Course(); var published = DateTimeOffset.UtcNow; var offer = Published(course, published); var now = published.AddMinutes(5);
        var fact = course.UnpublishOffer(offer.OfferId, now);
        Assert.Equal("unpublished", offer.Status); Assert.Equal(published, offer.PublishedAt); Assert.Equal(now, offer.UpdatedAt);
        Assert.Equal(3, fact.OfferRevision); Assert.Equal(3, offer.OfferRevision); Assert.Equal(now, fact.OccurredAt);
        Assert.Equal(offer.OfferId, fact.OfferId); Assert.Equal(course.CourseId, fact.CourseId); Assert.Equal(course.TenantId, fact.TenantId);
        Assert.Equal(offer.Name, fact.Name); Assert.Equal(offer.PriceCents, fact.PriceCents); Assert.Equal(offer.AccessPeriod, fact.AccessPeriod);
        Assert.Equal("BRL", fact.Currency);
    }

    [Theory(DisplayName = nameof(OnlyPublishedOfferCanBeUnpublished))]
    [InlineData("draft")]
    [InlineData("unpublished")]
    public void OnlyPublishedOfferCanBeUnpublished(string status)
    {
        var course = Course(); var offer = course.CreateOffer(new("Option", 49700, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
        if (status == "unpublished")
        {
            course.PublishOffer(offer.OfferId, DateTimeOffset.UtcNow); course.UnpublishOffer(offer.OfferId, DateTimeOffset.UtcNow);
        }
        var revision = offer.OfferRevision;
        var error = Assert.Throws<CatalogRuleException>(() => course.UnpublishOffer(offer.OfferId, DateTimeOffset.UtcNow));
        Assert.Equal("OFFER_STATE_CONFLICT", error.Code); Assert.Equal(status, offer.Status); Assert.Equal(revision, offer.OfferRevision);
    }

    [Fact(DisplayName = nameof(UnpublishingOneOfTwoKeepsTheShowcaseEntryMoment))]
    public void UnpublishingOneOfTwoKeepsTheShowcaseEntryMoment()
    {
        var course = Course(); var now = DateTimeOffset.UtcNow; var first = Published(course, now); Published(course, now.AddMinutes(1));
        course.UnpublishOffer(first.OfferId, now.AddMinutes(2));
        Assert.True(course.InShowcase); Assert.Equal(now, course.InShowcaseSince); Assert.Equal(1, course.Offers.Count(offer => offer.Status == "published"));
    }

    [Fact(DisplayName = nameof(UnpublishingTheLastPublishedOfferLeavesTheShowcase))]
    public void UnpublishingTheLastPublishedOfferLeavesTheShowcase()
    {
        var course = Course(); var now = DateTimeOffset.UtcNow; var offer = Published(course, now); Assert.True(course.InShowcase);
        course.UnpublishOffer(offer.OfferId, now.AddMinutes(1));
        Assert.False(course.InShowcase); Assert.Null(course.InShowcaseSince);
        course.PublishOffer(offer.OfferId, now.AddMinutes(2));
        Assert.Equal(now.AddMinutes(2), course.InShowcaseSince); Assert.Equal(4, offer.OfferRevision);
    }

    [Fact(DisplayName = nameof(UnpublishedOfferCannotBeDeleted))]
    public void UnpublishedOfferCannotBeDeleted()
    {
        var course = Course(); var offer = Published(course, DateTimeOffset.UtcNow); course.UnpublishOffer(offer.OfferId, DateTimeOffset.UtcNow);
        var error = Assert.Throws<CatalogRuleException>(() => course.DeleteOffer(offer.OfferId));
        Assert.Equal("OFFER_STATE_CONFLICT", error.Code); Assert.Single(course.Offers);
    }
}
