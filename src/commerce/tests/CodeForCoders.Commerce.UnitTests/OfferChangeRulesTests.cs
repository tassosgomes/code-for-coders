using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class OfferChangeRulesTests
{
    private static (CatalogCourseView Course, CatalogOffer Offer) Published()
    {
        var course = CatalogCourseView.Create(new(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "1.1.0",
            "Course", "Description", "beginner", "{}", "[]", DateTimeOffset.UtcNow));
        var offer = course.CreateOffer(new("Option", 49700, AccessPeriod.Create("months", 12)), DateTimeOffset.UtcNow);
        course.PublishOffer(offer.OfferId, DateTimeOffset.UtcNow);
        return (course, offer);
    }

    [Fact(DisplayName = nameof(PriceChangeEmitsPreviousAndCurrentPromise))]
    public void PriceChangeEmitsPreviousAndCurrentPromise()
    {
        var (course, offer) = Published(); var published = offer.PublishedAt; var now = DateTimeOffset.UtcNow.AddMinutes(1);
        var fact = course.UpdateOffer(offer.OfferId, new(null, 39700, null), now);
        Assert.NotNull(fact); Assert.Equal(49700, fact.Previous.PriceCents); Assert.Equal(39700, fact.PriceCents);
        Assert.Equal(fact.Previous.AccessPeriod, fact.AccessPeriod); Assert.Equal(3, fact.OfferRevision);
        Assert.Equal(now, fact.OccurredAt); Assert.Equal(course.CourseId, fact.CourseId); Assert.Equal(course.TenantId, fact.TenantId);
        Assert.Equal(offer.OfferId, fact.OfferId); Assert.Equal("Option", fact.Name); Assert.Equal("BRL", fact.Currency);
        Assert.Equal(published, offer.PublishedAt); Assert.Equal(published, course.InShowcaseSince);
    }

    [Fact(DisplayName = nameof(PeriodTypeChangeFreezesPreviousDuration))]
    public void PeriodTypeChangeFreezesPreviousDuration()
    {
        var (course, offer) = Published();
        var fact = course.UpdateOffer(offer.OfferId, new(null, null, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
        Assert.NotNull(fact); Assert.Equal(new("months", 12), fact.Previous.AccessPeriod);
        Assert.Equal(new("lifetime", null), fact.AccessPeriod); Assert.Equal(fact.Previous.PriceCents, fact.PriceCents);
    }

    [Fact(DisplayName = nameof(DurationChangeWithinMonthsEmitsAFact))]
    public void DurationChangeWithinMonthsEmitsAFact()
    {
        var (course, offer) = Published();
        var fact = course.UpdateOffer(offer.OfferId, new(null, null, AccessPeriod.Create("months", 6)), DateTimeOffset.UtcNow);
        Assert.NotNull(fact); Assert.Equal(12, fact.Previous.AccessPeriod.Months); Assert.Equal(6, fact.AccessPeriod.Months);
    }

    [Fact(DisplayName = nameof(CombinedChangesProduceOneRevisionAndAFactWithNewName))]
    public void CombinedChangesProduceOneRevisionAndAFactWithNewName()
    {
        var (course, offer) = Published();
        var fact = course.UpdateOffer(offer.OfferId, new("New name", 39700, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
        Assert.NotNull(fact); Assert.Equal(3, offer.OfferRevision); Assert.Equal("New name", fact.Name);
        Assert.Equal(49700, fact.Previous.PriceCents); Assert.Equal(new("months", 12), fact.Previous.AccessPeriod);
    }

    [Fact(DisplayName = nameof(NameOnlySavesWithoutAFact))]
    public void NameOnlySavesWithoutAFact()
    {
        var (course, offer) = Published();
        Assert.Null(course.UpdateOffer(offer.OfferId, new("Renamed", null, null), DateTimeOffset.UtcNow));
        Assert.Equal("Renamed", offer.Name); Assert.Equal(3, offer.OfferRevision);
    }

    [Fact(DisplayName = nameof(IdenticalValuesPreserveRevisionAndTimestamp))]
    public void IdenticalValuesPreserveRevisionAndTimestamp()
    {
        var (course, offer) = Published(); var updated = offer.UpdatedAt;
        Assert.Null(course.UpdateOffer(offer.OfferId, new(offer.Name, 49700, AccessPeriod.Create("months", 12)), updated.AddMinutes(1)));
        Assert.Equal(2, offer.OfferRevision); Assert.Equal(updated, offer.UpdatedAt);
    }

    [Theory(DisplayName = nameof(UnpublishedStatesEditSilently))]
    [InlineData("draft")]
    [InlineData("unpublished")]
    public void UnpublishedStatesEditSilently(string status)
    {
        var (course, offer) = Published();
        // Unpublication is delivered by task 8.0; emulate its persisted state.
        typeof(CatalogOffer).GetProperty(nameof(CatalogOffer.Status))!.SetValue(offer, status);
        Assert.Null(course.UpdateOffer(offer.OfferId, new(null, 39700, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow));
        Assert.Equal(39700, offer.PriceCents); Assert.Equal("lifetime", offer.AccessPeriod.Type); Assert.Equal(status, offer.Status);
    }

    [Fact(DisplayName = nameof(InvalidChangeLeavesAllPublishedTermsUntouched))]
    public void InvalidChangeLeavesAllPublishedTermsUntouched()
    {
        var (course, offer) = Published(); var updated = offer.UpdatedAt;
        Assert.Throws<CatalogRuleException>(() => course.UpdateOffer(offer.OfferId, new("Renamed", 0, AccessPeriod.Create("lifetime", null)), updated.AddMinutes(1)));
        Assert.Equal("Option", offer.Name); Assert.Equal(49700, offer.PriceCents); Assert.Equal(new("months", 12), offer.AccessPeriod);
        Assert.Equal(2, offer.OfferRevision); Assert.Equal(updated, offer.UpdatedAt);
    }
}
