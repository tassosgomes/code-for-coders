using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PurchaseSummaryTests(CommerceIntegrationFixture infra, CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture() { var tenant = Guid.CreateVersion7(); return new(hosts.Showcase(tenant), tenant); }
    [Fact(DisplayName = nameof(ShowsPublishedOfferAndPendingOrderOfThisOfferOnly))]
    public async Task ShowsPublishedOfferAndPendingOrderOfThisOfferOnly()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var created = await f.CreateAsync(ids[1], "pending"); var id = (await OrderFixture.BodyAsync(created)).GetProperty("orderId").GetGuid();
        var summary = await f.SummaryAsync(ids[1]); Assert.Equal(id, summary.GetProperty("pendingOrderId").GetGuid()); Assert.Equal(49700, summary.GetProperty("offer").GetProperty("priceCents").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await f.SummaryAsync(ids[2])).GetProperty("pendingOrderId").ValueKind);
    }
    [Fact(DisplayName = nameof(ShowsCourtesyWithLatestEndAndLifetimePrecedence))]
    public async Task ShowsCourtesyWithLatestEndAndLifetimePrecedence()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); await GrantAsync(f, ids[0], "months", 2); await GrantAsync(f, ids[0], "months", 5);
        var access = (await f.SummaryAsync(ids[1])).GetProperty("existingAccess"); Assert.Equal("courtesy", access.GetProperty("origin").GetString());
        Assert.Equal("until", access.GetProperty("validity").GetProperty("type").GetString());
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); var ends = await db.AccessGrants.MaxAsync(x => x.EndsOn, OrderFixture.Cancellation);
        Assert.Equal(ends!.Value.ToString("yyyy-MM-dd"), access.GetProperty("validity").GetProperty("endsOn").GetString());
        await GrantAsync(f, ids[0], "lifetime", null); Assert.Equal("lifetime", (await f.SummaryAsync(ids[1])).GetProperty("existingAccess").GetProperty("validity").GetProperty("type").GetString());
    }
    [Fact(DisplayName = nameof(ExpiredOrFutureGrantDoesNotProduceWarning))]
    public async Task ExpiredOrFutureGrantDoesNotProduceWarning()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); await GrantAsync(f, ids[0], "months", 1, DateTimeOffset.UtcNow.AddYears(-1)); await GrantAsync(f, ids[0], "months", 1, DateTimeOffset.UtcNow.AddYears(1));
        var summary = await f.SummaryAsync(ids[1]); Assert.True(summary.GetProperty("existingAccessChecked").GetBoolean()); Assert.Equal(System.Text.Json.JsonValueKind.Null, summary.GetProperty("existingAccess").ValueKind);
    }
    [Fact(DisplayName = nameof(EntitlementFailureReturnsSummaryWithUncheckedAccess))]
    public async Task EntitlementFailureReturnsSummaryWithUncheckedAccess()
    {
        var tenant = Guid.CreateVersion7(); using var factory = new ShowcaseApiFactory(infra, tenant);
        var f = new OrderFixture(factory, tenant); var ids = await f.SeedAsync();
        using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Scoped<IExistingCourseAccessReader, UnavailableExistingCourseAccessReader>())));
        using var tokenClient = f.Client(); using var client = failing.CreateClient(); client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync($"/internal/v1/offers/{ids[1]}/purchase-summary", OrderFixture.Cancellation);
        Assert.Equal(200, (int)response.StatusCode); var summary = await OrderFixture.BodyAsync(response); Assert.False(summary.GetProperty("existingAccessChecked").GetBoolean()); Assert.Equal(System.Text.Json.JsonValueKind.Null, summary.GetProperty("existingAccess").ValueKind);
    }
    [Fact(DisplayName = nameof(UnpublishedMissingAndForeignOffersReturn404))]
    public async Task UnpublishedMissingAndForeignOffersReturn404()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {ids[1]}", OrderFixture.Cancellation);
        var other = Fixture(); using var client = other.Client();
        foreach (var id in new[] { ids[1], ids[2], Guid.CreateVersion7() }) { using var response = await client.GetAsync($"/internal/v1/offers/{id}/purchase-summary", OrderFixture.Cancellation); Assert.Equal(404, (int)response.StatusCode); }
    }

    [Fact(DisplayName = nameof(CourtesyUntilNovember30IsShownUsingTheSchoolDate))]
    public async Task CourtesyUntilNovember30IsShownUsingTheSchoolDate()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); var clock = new CourtesyGrantTestClock();
        await GrantAsync(f, ids[0], "months", 2, clock.Now);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var end = new DateOnly(2026, 11, 30); var expires = DateTimeOffset.Parse("2026-12-01T03:00:00Z");
        await db.Database.ExecuteSqlAsync($"UPDATE entitlement.access_grants SET ends_on = {end}, expires_at = {expires} WHERE tenant_id = {f.Tenant}", OrderFixture.Cancellation);
        using var frozen = f.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));
        using var tokenClient = f.Client(); using var client = frozen.CreateClient(); client.DefaultRequestHeaders.Authorization = tokenClient.DefaultRequestHeaders.Authorization;
        using var response = await client.GetAsync($"/internal/v1/offers/{ids[1]}/purchase-summary", OrderFixture.Cancellation);
        Assert.Equal(200, (int)response.StatusCode); var summary = await OrderFixture.BodyAsync(response);
        Assert.Equal("2026-11-30", summary.GetProperty("existingAccess").GetProperty("validity").GetProperty("endsOn").GetString());
    }
    private static async Task GrantAsync(OrderFixture f, Guid course, string type, int? months, DateTimeOffset? at = null)
    {
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(x => x.StudentId == f.Student && x.CourseId == course, OrderFixture.Cancellation);
        if (enrollment is null) { enrollment = Enrollment.Create(f.Tenant, f.Student, course, at ?? DateTimeOffset.UtcNow); db.Enrollments.Add(enrollment); }
        db.AccessGrants.Add(AccessGrant.CreateCourtesy(enrollment, new(Guid.CreateVersion7(), "Courtesy", type, months, at ?? DateTimeOffset.UtcNow), TimeZoneInfo.FindSystemTimeZoneById("America/Fortaleza")));
        await db.SaveChangesAsync(OrderFixture.Cancellation);
    }
}
