using CodeForCoders.Commerce.Infra.Messaging;
using System.Net;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PurchaseIntentTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private readonly Guid tenant = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    [Fact(DisplayName = nameof(DeprecatedClicksReturnAvailableWithoutReceiptsOrCounting))]
    public async Task DeprecatedClicksReturnAvailableWithoutReceiptsOrCounting()
    {
        var factory = hosts.Showcase(tenant); var (course, offers) = await SeedAsync(factory, tenant);
        foreach (var key in new[] { "first", "first", "second" })
        {
            using var response = await SendAsync(factory, offers[0], key);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            Assert.Equal("{\"purchaseAvailability\":\"available\"}", await response.Content.ReadAsStringAsync(Cancellation));
        }
        await using var scope = Scope(factory, tenant); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Empty(await db.PurchaseIntentReceipts.ToListAsync(Cancellation)); Assert.Empty(await db.PurchaseIntentDailyCounts.ToListAsync(Cancellation));
        Assert.All((await RecordAsync(factory, course)).Offers, offer => Assert.Equal(0, offer.PurchaseIntentCount));
    }
    [Fact(DisplayName = nameof(UnpublishedMissingAndForeignOffersRemainUnavailable))]
    public async Task UnpublishedMissingAndForeignOffersRemainUnavailable()
    {
        var other = Guid.CreateVersion7(); var factory = hosts.Showcase(tenant, other); var (_, offers) = await SeedAsync(factory, tenant); var (_, foreign) = await SeedAsync(factory, other);
        await using var scope = Scope(factory, tenant); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {offers[0]}", Cancellation);
        foreach (var offer in new[] { offers[0], foreign[0], Guid.CreateVersion7() })
        { using var response = await SendAsync(factory, offer, "unavailable"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
        Assert.Empty(await db.PurchaseIntentDailyCounts.ToListAsync(Cancellation));
    }
    [Fact(DisplayName = nameof(InvalidKeysAndReadOnlyAssertionsRemainRejected))]
    public async Task InvalidKeysAndReadOnlyAssertionsRemainRejected()
    {
        var factory = hosts.Showcase(tenant); var (_, offers) = await SeedAsync(factory, tenant);
        foreach (var key in new[] { "", new string('a', 129) }) { using var response = await SendAsync(factory, offers[0], key); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using var missing = await SendAsync(factory, offers[0], "missing", null); Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        using var readOnly = await SendAsync(factory, offers[0], "readonly", "showcase:read"); Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
    }
    private async Task<HttpResponseMessage> SendAsync(ShowcaseApiFactory factory, Guid offerId, string key, string? scope = "purchase-intent:write")
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/showcase/offers/{offerId}/purchase-intents");
        if (scope is not null) request.Headers.Authorization = new("Bearer", factory.CreateAssertion(tenant, scope));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request, Cancellation);
    }

    private static AsyncServiceScope Scope(ShowcaseApiFactory factory, Guid school)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(school);
        return scope;
    }

    private async Task<CatalogCourseDetail> RecordAsync(ShowcaseApiFactory factory, Guid course)
    {
        await using var scope = Scope(factory, tenant);
        return (await scope.ServiceProvider.GetRequiredService<ICatalogCourseQueries>().GetAsync(course, Cancellation))!;
    }

    private static async Task<(Guid Course, Guid[] Offers)> SeedAsync(ShowcaseApiFactory factory, Guid school)
    {
        await using var scope = Scope(factory, school);
        var courseId = Guid.CreateVersion7();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(school, courseId, 1, rich: true, level: "beginner")), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.SingleAsync(item => item.CourseId == courseId, Cancellation);
        var now = DateTimeOffset.UtcNow;
        var monthly = course.CreateOffer(new("Monthly", 10000, AccessPeriod.Create("months", 12)), now);
        var lifetime = course.CreateOffer(new("Lifetime", 20000, AccessPeriod.Create("lifetime", null)), now);
        course.PublishOffer(monthly.OfferId, now);
        course.PublishOffer(lifetime.OfferId, now);
        await db.SaveChangesAsync(Cancellation);
        return (courseId, [monthly.OfferId, lifetime.OfferId]);
    }
}
