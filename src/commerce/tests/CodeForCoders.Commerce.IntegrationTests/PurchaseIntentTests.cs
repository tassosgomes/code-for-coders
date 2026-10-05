using System.Net;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PurchaseIntentTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private readonly Guid tenant = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(ClickCountsOnlyTheSelectedOfferAndReturnsTheContract))]
    public async Task ClickCountsOnlyTheSelectedOfferAndReturnsTheContract()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        using var response = await SendAsync(factory, offers[1], "one-click");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("{\"purchaseAvailability\":\"coming-soon\"}", await response.Content.ReadAsStringAsync(Cancellation));
        var record = await RecordAsync(factory, course);
        Assert.Equal([0L, 1L], offers.Select(id => record.Offers.Single(offer => offer.OfferId == id).PurchaseIntentCount));
    }

    [Fact(DisplayName = nameof(RepeatedKeyIsAcceptedWithoutCountingAgain))]
    public async Task RepeatedKeyIsAcceptedWithoutCountingAgain()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        using var first = await SendAsync(factory, offers[0], "retry");
        using var second = await SendAsync(factory, offers[0], "retry");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(1, (await RecordAsync(factory, course)).Offers.Single(offer => offer.OfferId == offers[0]).PurchaseIntentCount);
    }

    [Fact(DisplayName = nameof(ConcurrentRepetitionsCountExactlyOnce))]
    public async Task ConcurrentRepetitionsCountExactlyOnce()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync(factory, offers[0], "concurrent")));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); response.Dispose(); }
        Assert.Equal(1, (await RecordAsync(factory, course)).Offers.Single(offer => offer.OfferId == offers[0]).PurchaseIntentCount);
    }

    [Fact(DisplayName = nameof(ConcurrentDifferentClicksDoNotLoseIncrements))]
    public async Task ConcurrentDifferentClicksDoNotLoseIncrements()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => SendAsync(factory, offers[0], $"distinct-{index}")));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); response.Dispose(); }
        Assert.Equal(8, (await RecordAsync(factory, course)).Offers.Single(offer => offer.OfferId == offers[0]).PurchaseIntentCount);
    }

    [Fact(DisplayName = nameof(UnpublishedDraftUnknownAndOtherSchoolsOffersAreNotCounted))]
    public async Task UnpublishedDraftUnknownAndOtherSchoolsOffersAreNotCounted()
    {
        var other = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant, other);
        var (course, offers) = await SeedAsync(factory, tenant);
        var (_, others) = await SeedAsync(factory, other);
        await using var scope = Scope(factory, tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {offers[0]}", Cancellation);
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'draft' WHERE offer_id = {offers[1]}", Cancellation);
        foreach (var offer in new[] { offers[0], offers[1], others[0], Guid.CreateVersion7() })
        {
            using var response = await SendAsync(factory, offer, "not-available");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("OFFER_NOT_AVAILABLE", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        }
        Assert.All((await RecordAsync(factory, course)).Offers, offer => Assert.Equal(0, offer.PurchaseIntentCount));
        Assert.Empty(await db.PurchaseIntentReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(UnpublicationInProgressWinsBeforeTheClickAndLeavesTheCountUnchanged))]
    public async Task UnpublicationInProgressWinsBeforeTheClickAndLeavesTheCountUnchanged()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        await using var scope = Scope(factory, tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {offers[0]}", Cancellation);
        var click = SendAsync(factory, offers[0], "racing-unpublish");
        // A PostgreSQL observer proves the HTTP write reached the offer lock before committing unpublication.
        await using var observer = Scope(factory, tenant);
        var observerDb = observer.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var waiting = false;
        for (var attempt = 0; attempt < 100 && !waiting; attempt++)
        {
            waiting = await observerDb.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%purchase_intent_daily_counts%'").SingleAsync(Cancellation) > 0;
            if (!waiting) await Task.Delay(20, Cancellation);
        }
        Assert.True(waiting);
        await transaction.CommitAsync(Cancellation);
        using var response = await click;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, (await RecordAsync(factory, course)).Offers.Single(offer => offer.OfferId == offers[0]).PurchaseIntentCount);
    }

    [Fact(DisplayName = nameof(ReceiptStoresOnlyTheHashAndExpiresAfter24Hours))]
    public async Task ReceiptStoresOnlyTheHashAndExpiresAfter24Hours()
    {
        var factory = hosts.Showcase(tenant);
        var (_, offers) = await SeedAsync(factory, tenant);
        var before = DateTimeOffset.UtcNow;
        using var response = await SendAsync(factory, offers[0], "opaque-secret-click");
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var scope = Scope(factory, tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var receipt = await db.PurchaseIntentReceipts.SingleAsync(Cancellation);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("opaque-secret-click"))), receipt.KeyHash);
        Assert.InRange(receipt.ExpiresAt, before.AddHours(24), DateTimeOffset.UtcNow.AddHours(24));
        Assert.Equal(["ExpiresAt", "KeyHash", "OfferId", "TenantId"], typeof(CodeForCoders.Commerce.Domain.Entities.PurchaseIntentReceipt).GetProperties().Select(property => property.Name).Order());
        var count = await db.PurchaseIntentDailyCounts.SingleAsync(Cancellation);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), count.Day);
        Assert.Equal(["Count", "Day", "OfferId", "TenantId"], count.GetType().GetProperties().Select(property => property.Name).Order());
    }

    [Fact(DisplayName = nameof(ExpiredKeyCanCountAgainAndCatalogSumsEveryUtcDay))]
    public async Task ExpiredKeyCanCountAgainAndCatalogSumsEveryUtcDay()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        using var first = await SendAsync(factory, offers[0], "expired");
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        await using var scope = Scope(factory, tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var expired = DateTimeOffset.UtcNow.AddHours(-1);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.purchase_intent_receipts SET expires_at = {expired} WHERE offer_id = {offers[0]}", Cancellation);
        await db.Database.ExecuteSqlAsync($"INSERT INTO catalog.purchase_intent_daily_counts (tenant_id, offer_id, day, count) VALUES ({tenant}, {offers[0]}, {yesterday}, 14)", Cancellation);
        using var next = await SendAsync(factory, offers[0], "expired");
        Assert.Equal(HttpStatusCode.Accepted, next.StatusCode);
        Assert.Equal(16, (await RecordAsync(factory, course)).Offers.Single(offer => offer.OfferId == offers[0]).PurchaseIntentCount);
        Assert.Single(await db.PurchaseIntentReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingAssertionAndReadOnlyScopeCannotRegisterClicks))]
    public async Task MissingAssertionAndReadOnlyScopeCannotRegisterClicks()
    {
        var factory = hosts.Showcase(tenant);
        var (_, offers) = await SeedAsync(factory, tenant);
        using var anonymous = await SendAsync(factory, offers[0], "anonymous", scope: null);
        using var readOnly = await SendAsync(factory, offers[0], "read-only", scope: "showcase:read");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
    }

    [Fact(DisplayName = nameof(InvalidKeysDoNotProduceReceiptsOrCounts))]
    public async Task InvalidKeysDoNotProduceReceiptsOrCounts()
    {
        var factory = hosts.Showcase(tenant);
        var (course, offers) = await SeedAsync(factory, tenant);
        foreach (var key in new[] { "", new string('a', 129) })
        {
            using var response = await SendAsync(factory, offers[0], key);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.All((await RecordAsync(factory, course)).Offers, offer => Assert.Equal(0, offer.PurchaseIntentCount));
    }

    [Fact(DisplayName = nameof(BusinessTelemetryCountsAcceptedRepeatedAndRefusedClicksWithoutVisitorDataOrOfferDimensions))]
    public async Task BusinessTelemetryCountsAcceptedRepeatedAndRefusedClicksWithoutVisitorDataOrOfferDimensions()
    {
        var spans = new ConcurrentQueue<Activity>();
        var measurements = new ConcurrentQueue<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var activities = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CommerceTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => spans.Enqueue(activity),
        };
        ActivitySource.AddActivityListener(activities);
        using var meters = new MeterListener();
        meters.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == CommerceTelemetry.MeterName && instrument.Name.StartsWith("commerce.purchase_intent.", StringComparison.Ordinal))
                listener.EnableMeasurementEvents(instrument);
        };
        meters.SetMeasurementEventCallback<long>((instrument, value, tags, _) => measurements.Enqueue((instrument.Name, value, tags.ToArray())));
        meters.Start();
        var factory = hosts.Showcase(tenant);
        var (_, offers) = await SeedAsync(factory, tenant);
        using var counted = await SendAsync(factory, offers[0], "telemetry-private-key");
        using var repeated = await SendAsync(factory, offers[0], "telemetry-private-key");
        using var refused = await SendAsync(factory, Guid.CreateVersion7(), "telemetry-private-key");
        Assert.Equal(HttpStatusCode.Accepted, counted.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(["commerce.purchase_intent.counted", "commerce.purchase_intent.refused", "commerce.purchase_intent.repeated"], measurements.Select(item => item.Name).Order());
        Assert.All(measurements, item => { Assert.Equal(1, item.Value); Assert.Empty(item.Tags); });
        Assert.Equal(3, spans.Count(activity => activity.OperationName == "registerPurchaseIntent"));
        Assert.All(spans.Where(activity => activity.OperationName == "registerPurchaseIntent"), activity =>
        {
            Assert.Equal("offer.id", Assert.Single(activity.TagObjects).Key);
            Assert.DoesNotContain("telemetry-private-key", string.Join(';', activity.TagObjects), StringComparison.Ordinal);
        });
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
