using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Commerce.UnitTests;

public sealed class OfferRulesTests
{
    private static CatalogCourseView Course() => CatalogCourseView.Create(new(
        Guid.CreateVersion7(), Guid.CreateVersion7(), 1, "1.1.0", "Course", "Description", null, "{}", "[]", DateTimeOffset.UtcNow));
    private static OfferChange Terms(decimal price = 49700, AccessPeriod? period = null) => new("Option", price, period ?? AccessPeriod.Create("months", 12));

    [Theory(DisplayName = nameof(PriceAndPeriodAcceptInclusiveLimits))]
    [InlineData(1, 1)]
    [InlineData(9999999, 60)]
    public void PriceAndPeriodAcceptInclusiveLimits(int price, int months)
    {
        var course = Course(); var offer = course.CreateOffer(Terms(price, AccessPeriod.Create("months", months)), DateTimeOffset.UtcNow);
        Assert.Equal(price, offer.PriceCents); Assert.Equal(months, offer.AccessPeriod.Months);
        Assert.Equal("draft", offer.Status); Assert.Equal(1, offer.OfferRevision); Assert.Null(offer.PublishedAt);
    }

    [Theory(DisplayName = nameof(InvalidPriceNamesFieldAndLeavesAggregateUnchanged))]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000)]
    [InlineData(1.5)]
    public void InvalidPriceNamesFieldAndLeavesAggregateUnchanged(decimal price)
    {
        var course = Course(); var exception = Assert.Throws<CatalogRuleException>(() => course.CreateOffer(Terms(price), DateTimeOffset.UtcNow));
        Assert.Equal("FIELD_INVALID", exception.Code); Assert.Contains("priceCents", exception.Message); Assert.Empty(course.Offers);
    }

    [Theory(DisplayName = nameof(InvalidMonthsNamesAccessPeriod))]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(1.5)]
    public void InvalidMonthsNamesAccessPeriod(decimal months)
    {
        var exception = Assert.Throws<CatalogRuleException>(() => AccessPeriod.Create("months", months));
        Assert.Equal("FIELD_INVALID", exception.Code); Assert.Contains("accessPeriod", exception.Message);
    }

    [Theory(DisplayName = nameof(NameAcceptsInclusiveLimits))]
    [InlineData(1)]
    [InlineData(60)]
    public void NameAcceptsInclusiveLimits(int length)
    {
        var offer = Course().CreateOffer(Terms() with { Name = new string('a', length) }, DateTimeOffset.UtcNow);
        Assert.Equal(length, offer.Name.Length);
    }

    [Theory(DisplayName = nameof(InvalidNameNamesField))]
    [InlineData(0)]
    [InlineData(61)]
    public void InvalidNameNamesField(int length)
    {
        var exception = Assert.Throws<CatalogRuleException>(() => Course().CreateOffer(Terms() with { Name = new string('a', length) }, DateTimeOffset.UtcNow));
        Assert.Contains("name", exception.Message);
    }

    [Fact(DisplayName = nameof(LifetimeAndMonthsCoexistWithoutCourseLevel))]
    public void LifetimeAndMonthsCoexistWithoutCourseLevel()
    {
        var course = Course(); var monthly = course.CreateOffer(Terms(), DateTimeOffset.UtcNow);
        var lifetime = course.CreateOffer(Terms(89700, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
        Assert.Equal(2, course.Offers.Count); Assert.Equal(49700, monthly.PriceCents); Assert.Null(lifetime.AccessPeriod.Months);
    }

    [Theory(DisplayName = nameof(InvalidPeriodShapeIsRejectedByDomain))]
    [InlineData("lifetime", 12)]
    [InlineData("days", 12)]
    [InlineData("months", null)]
    public void InvalidPeriodShapeIsRejectedByDomain(string type, int? months)
        => Assert.Throws<CatalogRuleException>(() => AccessPeriod.Create(type, months));

    [Fact(DisplayName = nameof(FiftyOffersLimitIncludesEveryStateAndDeletionFreesSlot))]
    public void FiftyOffersLimitIncludesEveryStateAndDeletionFreesSlot()
    {
        var course = Course();
        for (var index = 0; index < 50; index++) course.CreateOffer(Terms(), DateTimeOffset.UtcNow);
        var exception = Assert.Throws<CatalogRuleException>(() => course.CreateOffer(Terms(), DateTimeOffset.UtcNow));
        Assert.Contains("offers", exception.Message); Assert.Equal("FIELD_INVALID", exception.Code);
        course.DeleteOffer(course.Offers.First().OfferId); course.CreateOffer(Terms(), DateTimeOffset.UtcNow); Assert.Equal(50, course.Offers.Count);
    }

    [Fact(DisplayName = nameof(IdenticalEditKeepsRevisionAndTimestampAndChangedEditIncrementsOnce))]
    public void IdenticalEditKeepsRevisionAndTimestampAndChangedEditIncrementsOnce()
    {
        var course = Course(); var now = DateTimeOffset.UtcNow; var offer = course.CreateOffer(Terms(), now);
        course.UpdateOffer(offer.OfferId, Terms(), now.AddMinutes(1)); Assert.Equal(1, offer.OfferRevision); Assert.Equal(now, offer.UpdatedAt);
        course.UpdateOffer(offer.OfferId, new("Renamed", 89700, AccessPeriod.Create("lifetime", null)), now.AddMinutes(2));
        Assert.Equal(2, offer.OfferRevision); Assert.Equal(now.AddMinutes(2), offer.UpdatedAt); Assert.Equal("draft", offer.Status);
    }

    [Fact(DisplayName = nameof(InvalidEditDoesNotChangeAnyField))]
    public void InvalidEditDoesNotChangeAnyField()
    {
        var course = Course(); var offer = course.CreateOffer(Terms(), DateTimeOffset.UtcNow);
        Assert.Throws<CatalogRuleException>(() => course.UpdateOffer(offer.OfferId, new("Changed", 0, null), DateTimeOffset.UtcNow));
        Assert.Equal("Option", offer.Name); Assert.Equal(49700, offer.PriceCents); Assert.Equal(1, offer.OfferRevision);
    }

    [Theory(DisplayName = nameof(NonDraftCannotBeDeleted))]
    [InlineData("published")]
    [InlineData("unpublished")]
    public void NonDraftCannotBeDeleted(string status)
    {
        var course = Course(); var offer = course.CreateOffer(Terms(), DateTimeOffset.UtcNow);
        // Publication belongs to the next slice; emulate an already persisted state for this invariant.
        typeof(CatalogOffer).GetProperty(nameof(CatalogOffer.Status))!.SetValue(offer, status);
        var exception = Assert.Throws<CatalogRuleException>(() => course.DeleteOffer(offer.OfferId));
        Assert.Equal("OFFER_STATE_CONFLICT", exception.Code); Assert.Single(course.Offers);
    }
}
