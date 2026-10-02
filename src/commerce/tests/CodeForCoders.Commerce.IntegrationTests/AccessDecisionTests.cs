using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.CreateOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.PublishOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UpdateOffer;
using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
[Trait("Api", "AccessDecision - Integration")]
public sealed class AccessDecisionTests(CommerceIntegrationFixture infra)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(SixMonthCourtesyAllowsUntilItsExpiry))]
    public async Task SixMonthCourtesyAllowsUntilItsExpiry()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        var grant = await test.GrantAsync();
        var decision = await test.DecideAsync();
        Assert.Equal("allowed", decision.Decision);
        Assert.Equal("until", decision.Validity!.Type);
        Assert.Equal(grant.ExpiresAt, decision.Validity.ExpiresAt);
        Assert.Null(decision.DeniedReason);
        Assert.Null(decision.LastExpiredAt);
    }

    [Theory(DisplayName = nameof(UnknownAccountsCoursesAndOtherSchoolsAreIndistinguishable))]
    [InlineData("no-grants")]
    [InlineData("unknown-student")]
    [InlineData("staff-account")]
    [InlineData("unknown-course")]
    [InlineData("other-school")]
    public async Task UnknownAccountsCoursesAndOtherSchoolsAreIndistinguishable(string scenario)
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        if (scenario != "no-grants") await test.GrantAsync();
        var student = scenario == "unknown-student" ? Guid.CreateVersion7() : scenario == "staff-account" ? test.Courtesy.Actor : test.Courtesy.Student;
        var decision = await test.DecideAsync(student, scenario == "unknown-course" ? Guid.CreateVersion7() : test.Courtesy.Course,
            scenario == "other-school" ? test.OtherTenant : test.Courtesy.Tenant);
        Assert.Equal("denied", decision.Decision);
        Assert.Equal("no-grant", decision.DeniedReason);
        Assert.Null(decision.Validity);
        Assert.Null(decision.LastExpiredAt);
    }

    [Fact(DisplayName = nameof(ExpiredYesterdayDeniesWithoutRunningAnyExpirationRoutine))]
    public async Task ExpiredYesterdayDeniesWithoutRunningAnyExpirationRoutine()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        var grant = await test.GrantAsync();
        test.Courtesy.Clock.Now = grant.ExpiresAt!.Value.AddDays(1);
        var decision = await test.DecideAsync();
        Assert.Equal("denied", decision.Decision);
        Assert.Equal("grant-ended", decision.DeniedReason);
        Assert.Equal(grant.ExpiresAt, decision.LastExpiredAt);
        Assert.Null(decision.Validity);
        await using var scope = Scope(test);
        var stored = await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.SingleAsync(Cancellation);
        Assert.Equal("active", stored.Status);
        Assert.Null(stored.ExpiryEventId);
        Assert.Null(stored.ExpiryPublishedAt);
    }

    [Theory(DisplayName = nameof(LastNightEndsExactlyAtTheFollowingMidnight))]
    [InlineData(-1, "allowed")]
    [InlineData(0, "denied")]
    public async Task LastNightEndsExactlyAtTheFollowingMidnight(int offset, string expected)
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        var grant = await test.GrantAsync();
        test.Courtesy.Clock.Now = grant.ExpiresAt!.Value.AddSeconds(offset);
        var zone = test.Courtesy.Factory.Services.GetRequiredService<SchoolTimeZone>().Zone;
        var local = TimeZoneInfo.ConvertTime(test.Courtesy.Clock.Now, zone);
        Assert.Equal(offset == -1 ? new TimeOnly(23, 59, 59) : TimeOnly.MinValue, TimeOnly.FromDateTime(local.DateTime));
        var result = await test.DecideAsync();
        Assert.Equal(expected, result.Decision);
        if (offset == 0) Assert.Equal("grant-ended", result.DeniedReason);
    }

    [Fact(DisplayName = nameof(ExpiredAndActiveGrantsAllowByTheLongestActiveExpiry))]
    public async Task ExpiredAndActiveGrantsAllowByTheLongestActiveExpiry()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        var expired = await test.GrantAsync(1);
        var longest = await test.GrantAsync(6);
        await test.GrantAsync(3);
        test.Courtesy.Clock.Now = expired.ExpiresAt!.Value;
        var decision = await test.DecideAsync();
        Assert.Equal("allowed", decision.Decision);
        Assert.Equal(longest.ExpiresAt, decision.Validity!.ExpiresAt);
        Assert.Null(decision.LastExpiredAt);
    }

    [Fact(DisplayName = nameof(AllExpiredGrantsReportTheLatestExpiry))]
    public async Task AllExpiredGrantsReportTheLatestExpiry()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        var longest = await test.GrantAsync(6);
        await test.GrantAsync(3);
        test.Courtesy.Clock.Now = longest.ExpiresAt!.Value;
        var decision = await test.DecideAsync();
        Assert.Equal("grant-ended", decision.DeniedReason);
        Assert.Equal(longest.ExpiresAt, decision.LastExpiredAt);
    }

    [Fact(DisplayName = nameof(LifetimeDominatesThreeMonthsAndHasNoExpiresAtProperty))]
    public async Task LifetimeDominatesThreeMonthsAndHasNoExpiresAtProperty()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        await test.GrantAsync(3);
        await test.GrantAsync(type: "lifetime");
        var decision = await test.DecideAsync();
        Assert.Equal("allowed", decision.Decision);
        Assert.Equal("lifetime", decision.Validity!.Type);
        Assert.Null(decision.Validity.ExpiresAt);
        test.Courtesy.Clock.Now = test.Courtesy.Clock.Now.AddYears(20);
        Assert.Equal("lifetime", (await test.DecideAsync()).Validity!.Type);
    }

    [Fact(DisplayName = nameof(InactiveGrantsDoNotAuthorizeOrOverrideTheActiveExpiry))]
    public async Task InactiveGrantsDoNotAuthorizeOrOverrideTheActiveExpiry()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        await test.GrantAsync(type: "lifetime");
        await using var scope = Scope(test);
        await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().AccessGrants.ExecuteUpdateAsync(
            update => update.SetProperty(grant => grant.Status, "inactive"), Cancellation);
        Assert.Equal("no-grant", (await test.DecideAsync()).DeniedReason);
        var active = await test.GrantAsync();
        Assert.Equal(active.ExpiresAt, (await test.DecideAsync()).Validity!.ExpiresAt);
    }

    [Fact(DisplayName = nameof(NewCourseVersionOfferChangesAndUnpublishingPreserveTheDecision))]
    public async Task NewCourseVersionOfferChangesAndUnpublishingPreserveTheDecision()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        await test.GrantAsync();
        var original = await test.DecideAsync();
        await using var scope = Scope(test);
        var services = scope.ServiceProvider;
        var fact = PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(test.Courtesy.Tenant, test.Courtesy.Course, rich: true));
        await services.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(fact, Cancellation);
        var offer = await services.GetRequiredService<ICreateOffer>().ExecuteAsync(new(test.Courtesy.Tenant, test.Courtesy.Actor,
            test.Courtesy.Course, "decision-offer", JsonSerializer.SerializeToElement(new
            { name = "Course offer", priceCents = 49700, accessPeriod = new { type = "months", months = 3 } })), Cancellation);
        await services.GetRequiredService<IPublishOffer>().ExecuteAsync(new(test.Courtesy.Tenant, test.Courtesy.Actor,
            offer.OfferId, "decision-publish", null), Cancellation);
        Assert.Equal(original, await test.DecideAsync());
        var next = PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(test.Courtesy.Tenant, test.Courtesy.Course,
            version: 2, rich: true, title: "Updated course title"));
        await services.GetRequiredService<IEntitlementCourseProjectionStore>().ApplyAsync(next, Cancellation);
        await services.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(next, Cancellation);
        Assert.Equal(original, await test.DecideAsync());
        await services.GetRequiredService<IUpdateOffer>().ExecuteAsync(new(test.Courtesy.Tenant, test.Courtesy.Actor,
            offer.OfferId, "decision-update", JsonSerializer.SerializeToElement(new
            { priceCents = 39700, accessPeriod = new { type = "lifetime" } })), Cancellation);
        Assert.Equal(original, await test.DecideAsync());
        await services.GetRequiredService<IUnpublishOffer>().ExecuteAsync(new(test.Courtesy.Tenant, test.Courtesy.Actor,
            offer.OfferId, "decision-unpublish", null), Cancellation);
        Assert.Equal(original, await test.DecideAsync());
    }

    [Fact(DisplayName = nameof(DecisionDoesNotReadIdentityOrCourseProjectionAndDoesNotLeakPersonIdentifiers))]
    public async Task DecisionDoesNotReadIdentityOrCourseProjectionAndDoesNotLeakPersonIdentifiers()
    {
        await using var test = new AccessDecisionFixture(infra);
        await test.Courtesy.SeedAsync();
        await test.GrantAsync();
        var identityCalls = test.Courtesy.Identity.Calls;
        test.Courtesy.Identity.Unavailable = true;
        await using var scope = Scope(test);
        await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().EntitlementCourseViews.ExecuteDeleteAsync(Cancellation);
        test.Courtesy.Logs.Clear(); test.Courtesy.Spans.Clear(); test.Measurements.Clear();
        Assert.Equal("allowed", (await test.DecideAsync()).Decision);
        Assert.Equal(identityCalls, test.Courtesy.Identity.Calls);
        var telemetry = string.Join(" ", test.Courtesy.Logs.Concat(test.Courtesy.Spans).Concat(test.Measurements));
        Assert.DoesNotContain(test.Courtesy.Student.ToString("D"), telemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(test.Courtesy.Actor.ToString("D"), telemetry, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(test.Courtesy.Student.ToString("N"), telemetry, StringComparison.OrdinalIgnoreCase);
    }

    [Theory(DisplayName = nameof(InvalidServiceCredentialsCannotAuthorizeDecision))]
    [InlineData("missing", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("scope", 403, "SCOPE_DENIED")]
    [InlineData("issuer-scope", 403, "SCOPE_DENIED")]
    [InlineData("tenant", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("bff-student", 403, "SCOPE_DENIED")]
    [InlineData("actor", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("audience", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("expired", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("lifetime", 401, "SERVICE_UNAUTHORIZED")]
    [InlineData("key", 401, "SERVICE_UNAUTHORIZED")]
    public async Task InvalidServiceCredentialsCannotAuthorizeDecision(string scenario, int status, string code)
    {
        await using var test = new AccessDecisionFixture(infra);
        var token = scenario switch
        {
            "missing" => null,
            "scope" => test.Assertion(scope: ServiceAssertionScopes.ShowcaseRead),
            "issuer-scope" => test.Assertion(scope: ServiceAssertionScopes.PurchaseIntentWrite),
            "tenant" => test.Assertion(tenant: Guid.CreateVersion7()),
            "bff-student" => test.Assertion(issuer: "bff-student"),
            "actor" => test.Courtesy.Client.DefaultRequestHeaders.Authorization!.Parameter,
            "audience" => test.Assertion(audience: "identity"),
            "expired" => test.Assertion(lifetimeSeconds: 0),
            "lifetime" => test.Assertion(lifetimeSeconds: 61),
            "key" => test.Assertion(keyId: "unregistered-key"),
            _ => throw new NotSupportedException()
        };
        using var response = await test.RequestAsync(token);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(AssertionReplayIsRejectedByTheRealValkeyStore))]
    public async Task AssertionReplayIsRejectedByTheRealValkeyStore()
    {
        await using var test = new AccessDecisionFixture(infra);
        await using var scope = Scope(test);
        Assert.IsType<ServiceAssertionReplayStore>(scope.ServiceProvider.GetRequiredService<IServiceAssertionReplayStore>());
        var token = test.Assertion();
        using var first = await test.RequestAsync(token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var replay = await test.RequestAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Contains("SERVICE_UNAUTHORIZED", await replay.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Theory(DisplayName = nameof(ServiceAssertionIsRefusedByCourtesyActorRoutes))]
    [InlineData("/internal/v1/courtesy-courses")]
    [InlineData("/internal/v1/courtesy-term-preview?months=6")]
    [InlineData("/internal/v1/courtesy-grants/00000000-0000-7000-8000-000000000001")]
    [InlineData("/internal/v1/students/00000000-0000-7000-8000-000000000001/access-grants")]
    public async Task ServiceAssertionIsRefusedByCourtesyActorRoutes(string path)
    {
        await using var test = new AccessDecisionFixture(infra);
        using var response = await test.RequestAsync(test.Assertion(), path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = nameof(ServiceAssertionCannotCreateCourtesyGrants))]
    public async Task ServiceAssertionCannotCreateCourtesyGrants()
    {
        await using var test = new AccessDecisionFixture(infra);
        test.Courtesy.Client.DefaultRequestHeaders.Authorization = new("Bearer", test.Assertion());
        using var response = await test.Courtesy.GrantAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await test.Courtesy.AssertEmptyAsync();
    }

    [Theory(DisplayName = nameof(MissingAndMalformedQueryIdentifiersAreRejected))]
    [InlineData("?studentId=invalid&courseId=invalid")]
    [InlineData("?studentId=00000000-0000-7000-8000-000000000001")]
    [InlineData("?courseId=00000000-0000-7000-8000-000000000001")]
    public async Task MissingAndMalformedQueryIdentifiersAreRejected(string query)
    {
        await using var test = new AccessDecisionFixture(infra);
        using var response = await test.RequestAsync(test.Assertion(), "/internal/v1/access-decision" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static AsyncServiceScope Scope(AccessDecisionFixture test)
    {
        var scope = test.Courtesy.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(test.Courtesy.Tenant);
        return scope;
    }

}
