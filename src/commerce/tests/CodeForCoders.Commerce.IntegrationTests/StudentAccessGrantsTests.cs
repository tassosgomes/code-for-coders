using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class StudentAccessGrantsTests(CommerceIntegrationFixture infra)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static string ListPath(CourtesyGrantFixture test) => $"/internal/v1/students/{test.Student}/access-grants";
    private static async Task<StudentAccessGrantPage> ListAsync(CourtesyGrantFixture test, string query = "")
    {
        using var response = await test.Client.GetAsync(ListPath(test) + query, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentAccessGrantPage>(Cancellation))!;
    }
    private static async Task<StudentAccessGrant> GrantAsync(CourtesyGrantFixture test, string key, bool lifetime = false)
    {
        object body = lifetime ? new { studentId = test.Student, courseId = test.Course, accessPeriod = new { type = "lifetime" }, reason = "Lifetime scholarship" } : test.Body();
        using var response = await test.GrantAsync(body, key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StudentAccessGrant>(Cancellation))!;
    }
    [Theory(DisplayName = nameof(StatusIsCalculatedAtReadWithoutExpirationWorker))]
    [InlineData(-1, "active")]
    [InlineData(0, "expired")]
    [InlineData(1, "expired")]
    public async Task StatusIsCalculatedAtReadWithoutExpirationWorker(int secondsFromExpiry, string expected)
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        var grant = await GrantAsync(test, "expiry"); test.Clock.Now = grant.ExpiresAt!.Value.AddSeconds(secondsFromExpiry);
        var page = await ListAsync(test); Assert.Equal(expected, Assert.Single(page.Data).Status);
        await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var stored = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.SingleAsync(Cancellation);
        Assert.Equal("active", stored.Status); Assert.Null(stored.ExpiryEventId);
    }
    [Fact(DisplayName = nameof(LifetimeAndLatestCourseTitleAreReadFromCurrentProjection))]
    public async Task LifetimeAndLatestCourseTitleAreReadFromCurrentProjection()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); await GrantAsync(test, "lifetime", true);
        test.Clock.Now = test.Clock.Now.AddYears(20);
        await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().EntitlementCourseViews.ExecuteUpdateAsync(update => update.SetProperty(course => course.Title, "Current course title"), Cancellation);
        var grant = Assert.Single((await ListAsync(test)).Data);
        Assert.Equal("Current course title", grant.CourseTitle); Assert.Equal("active", grant.Status); Assert.Null(grant.EndsOn); Assert.Null(grant.ExpiresAt);
    }
    [Theory(DisplayName = nameof(ListResponseMatchesPublishedSchemaAndRejectsInvalidAccessPeriod))]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListResponseMatchesPublishedSchemaAndRejectsInvalidAccessPeriod(bool lifetime)
    {
        await using var test = new CourtesyGrantFixture(infra);
        await test.SeedAsync();
        await GrantAsync(test, "schema", lifetime);
        using var response = await test.Client.GetAsync(ListPath(test), Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = (await response.Content.ReadFromJsonAsync<JsonObject>(Cancellation))!;
        Assert.True(StudentAccessGrantContract.IsValid(payload), "HTTP grant list must match the published OpenAPI AccessGrantPage schema.");
        var period = Assert.Single(payload["data"]!.AsArray())!["accessPeriod"]!.AsObject();
        Assert.Equal(lifetime ? "lifetime" : "months", period["type"]!.GetValue<string>());
        if (lifetime)
        {
            Assert.False(period.ContainsKey("months"));
            period["months"] = null;
        }
        else
        {
            Assert.Equal(6, period["months"]!.GetValue<int>());
            period.Remove("months");
        }
        Assert.False(StudentAccessGrantContract.IsValid(payload), "The published schema must reject months:null for lifetime and missing months for a fixed period.");
    }
    [Fact(DisplayName = nameof(PaginationOrdersNewestFirstWithStableIdTieBreaker))]
    public async Task PaginationOrdersNewestFirstWithStableIdTieBreaker()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        var first = await GrantAsync(test, "first"); var second = await GrantAsync(test, "second");
        test.Clock.Now = test.Clock.Now.AddMinutes(1); var third = await GrantAsync(test, "third");
        var expected = new[] { first, second, third }.OrderByDescending(grant => grant.GrantedAt).ThenByDescending(grant => grant.GrantId).Select(grant => grant.GrantId).ToArray();
        var pages = new List<Guid>();
        for (var number = 1; number <= 3; number++)
        {
            var page = await ListAsync(test, $"?_page={number}&_size=1"); Assert.Equal(3, page.Pagination.Total); Assert.Equal(3, page.Pagination.TotalPages);
            Assert.Equal(number, page.Pagination.Page); Assert.Equal(1, page.Pagination.Size); pages.Add(Assert.Single(page.Data).GrantId);
        }
        Assert.Equal(expected, pages); Assert.Empty((await ListAsync(test, "?_page=4&_size=1")).Data);
    }
    [Fact(DisplayName = nameof(AllOriginsAreListedAndNonCourtesyReasonIsNull))]
    public async Task AllOriginsAreListedAndNonCourtesyReasonIsNull()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        foreach (var origin in new[] { "courtesy", "purchase", "subscription", "cohort" })
        {
            var grant = await GrantAsync(test, origin);
            await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
            await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.Where(row => row.Id == grant.GrantId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Origin, origin), Cancellation);
        }
        var page = await ListAsync(test); Assert.Equal(4, page.Data.Count);
        Assert.Equal(new[] { "cohort", "courtesy", "purchase", "subscription" }, page.Data.Select(grant => grant.Origin).Order().ToArray());
        Assert.All(page.Data.Where(grant => grant.Origin != "courtesy"), grant => Assert.Null(grant.Reason));
    }
    [Theory(DisplayName = nameof(UnknownEmptyAndOtherSchoolStudentsReturnEmptyPage))]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("other-school")]
    public async Task UnknownEmptyAndOtherSchoolStudentsReturnEmptyPage(string scenario)
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync();
        if (scenario != "empty") await GrantAsync(test, "seed");
        if (scenario == "other-school") test.Authorize(Guid.CreateVersion7());
        var path = scenario == "unknown" ? $"/internal/v1/students/{Guid.CreateVersion7()}/access-grants" : ListPath(test);
        using var response = await test.Client.GetAsync(path, Cancellation); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<StudentAccessGrantPage>(Cancellation))!;
        Assert.Empty(page.Data); Assert.Equal(0, page.Pagination.Total); Assert.Equal(0, page.Pagination.TotalPages);
    }
    [Fact(DisplayName = nameof(SecondCourtesyCoexistsOnSameEnrollmentWithoutChangingFirst))]
    public async Task SecondCourtesyCoexistsOnSameEnrollmentWithoutChangingFirst()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); var first = await GrantAsync(test, "first");
        test.Clock.Now = test.Clock.Now.AddDays(1); var second = await GrantAsync(test, "second");
        var page = await ListAsync(test); Assert.Equal(2, page.Data.Count); Assert.Equal(second.GrantId, page.Data[0].GrantId);
        Assert.Equal(first, page.Data.Single(grant => grant.GrantId == first.GrantId)); Assert.All(page.Data, grant => Assert.Equal("active", grant.Status));
        await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); Assert.Single(await db.Enrollments.ToListAsync(Cancellation));
        Assert.Single((await db.AccessGrants.ToListAsync(Cancellation)).Select(grant => grant.EnrollmentId).Distinct());
    }
    [Theory(DisplayName = nameof(CourtesyReadHidesUnknownOtherOriginAndOtherSchoolGrants))]
    [InlineData("unknown")]
    [InlineData("other-origin")]
    [InlineData("other-school")]
    public async Task CourtesyReadHidesUnknownOtherOriginAndOtherSchoolGrants(string scenario)
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); var grant = await GrantAsync(test, "seed");
        if (scenario == "other-origin")
        {
            await using var scope = test.Factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Tenant);
            await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.ExecuteUpdateAsync(update => update.SetProperty(row => row.Origin, "purchase"), Cancellation);
        }
        if (scenario == "other-school") test.Authorize(Guid.CreateVersion7());
        using var response = await test.Client.GetAsync($"/internal/v1/courtesy-grants/{(scenario == "unknown" ? Guid.CreateVersion7() : grant.GrantId)}", Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Contains("GRANT_NOT_FOUND", await response.Content.ReadAsStringAsync(Cancellation));
    }
    [Fact(DisplayName = nameof(CourtesyReadReturnsOnlyOwnSchoolCourtesyWithCurrentStatus))]
    public async Task CourtesyReadReturnsOnlyOwnSchoolCourtesyWithCurrentStatus()
    {
        await using var test = new CourtesyGrantFixture(infra); await test.SeedAsync(); var grant = await GrantAsync(test, "seed");
        test.Clock.Now = grant.ExpiresAt!.Value;
        using var response = await test.Client.GetAsync($"/internal/v1/courtesy-grants/{grant.GrantId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); var read = (await response.Content.ReadFromJsonAsync<StudentAccessGrant>(Cancellation))!;
        Assert.Equal("courtesy", read.Origin); Assert.Equal("expired", read.Status); Assert.Equal(grant.GrantId, read.GrantId);
    }
    [Fact(DisplayName = nameof(SessionPermissionAndPaginationAreEnforced))]
    public async Task SessionPermissionAndPaginationAreEnforced()
    {
        await using var test = new CourtesyGrantFixture(infra); test.Authorize(permission: "financeiro.ler");
        using var forbidden = await test.Client.GetAsync(ListPath(test), Cancellation); Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        test.Client.DefaultRequestHeaders.Authorization = null;
        using var unauthorized = await test.Client.GetAsync(ListPath(test), Cancellation); Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        test.Authorize();
        foreach (var query in new[] { "?_page=0", "?_size=51", "?_size=0", "?_page=2147483647&_size=50" })
        {
            using var invalid = await test.Client.GetAsync(ListPath(test) + query, Cancellation); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }
}
