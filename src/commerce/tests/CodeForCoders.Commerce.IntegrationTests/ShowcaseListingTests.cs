using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class ShowcaseListingTests(CommerceIntegrationFixture fixture)
{
    private const string Route = "/internal/v1/showcase/courses";

    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid tenant = Guid.CreateVersion7();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(OnlyCoursesWithLevelAndAPublishedOfferAppear))]
    public async Task OnlyCoursesWithLevelAndAPublishedOfferAppear()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var visible = await SeedAsync(factory, tenant, "Visible", "beginner", published: [49700]);
        await SeedAsync(factory, tenant, "Draft only", "beginner", draft: [10000]);
        await SeedAsync(factory, tenant, "No offers", "beginner");
        await SeedAsync(factory, tenant, "Unpublished", "beginner", published: [10000], unpublishAll: true);
        await SeedAsync(factory, tenant, "No level", level: null, published: [10000], offersWhileLevelPresent: true);

        var page = await ListAsync(factory, tenant);

        var card = Assert.Single(page.GetProperty("data").EnumerateArray());
        Assert.Equal(visible, card.GetProperty("courseId").GetGuid());
        Assert.Equal("Visible", card.GetProperty("title").GetString());
        Assert.Equal("beginner", card.GetProperty("level").GetString());
        Assert.Equal(1, page.GetProperty("pagination").GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = nameof(CardsAreOrderedByShowcaseEntryDescendingThenCourseId))]
    public async Task CardsAreOrderedByShowcaseEntryDescendingThenCourseId()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var oldest = await SeedAsync(factory, tenant, "Oldest", "beginner", published: [100], at: Start);
        var newest = await SeedAsync(factory, tenant, "Newest", "advanced", published: [100], at: Start.AddHours(2));
        var tieA = await SeedAsync(factory, tenant, "Tie A", "beginner", published: [100], at: Start.AddHours(1));
        var tieB = await SeedAsync(factory, tenant, "Tie B", "beginner", published: [100], at: Start.AddHours(1));

        var page = await ListAsync(factory, tenant);

        var expected = new[] { newest }.Concat(new[] { tieA, tieB }.Order()).Append(oldest);
        Assert.Equal(expected, page.GetProperty("data").EnumerateArray().Select(card => card.GetProperty("courseId").GetGuid()));
    }

    [Theory(DisplayName = nameof(LevelFilterKeepsOnlyThatLevel))]
    [InlineData("beginner")]
    [InlineData("intermediate")]
    [InlineData("advanced")]
    public async Task LevelFilterKeepsOnlyThatLevel(string level)
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        foreach (var each in new[] { "beginner", "intermediate", "advanced" })
        {
            await SeedAsync(factory, tenant, $"Course {each}", each, published: [100]);
        }

        var page = await ListAsync(factory, tenant, $"?level={level}");

        var card = Assert.Single(page.GetProperty("data").EnumerateArray());
        Assert.Equal(level, card.GetProperty("level").GetString());
        Assert.Equal($"Course {level}", card.GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(FilterWithoutResultsReturnsAnEmptyPage))]
    public async Task FilterWithoutResultsReturnsAnEmptyPage()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        await SeedAsync(factory, tenant, "Only beginner", "beginner", published: [100]);

        var page = await ListAsync(factory, tenant, "?level=advanced");

        Assert.Empty(page.GetProperty("data").EnumerateArray());
        Assert.Equal(0, page.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal(0, page.GetProperty("pagination").GetProperty("totalPages").GetInt32());
    }

    [Theory(DisplayName = nameof(InvalidQueryIsRefusedWithInvalidRequest))]
    [InlineData("?level=xyz")]
    [InlineData("?level=Beginner")]
    [InlineData("?level=")]
    [InlineData("?_size=49")]
    [InlineData("?_size=0")]
    [InlineData("?_page=0")]
    public async Task InvalidQueryIsRefusedWithInvalidRequest(string query)
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);

        using var response = await GetAsync(factory, tenant, query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_REQUEST", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(PaginationDefaultsToTwelveAndHonoursPageAndSize))]
    public async Task PaginationDefaultsToTwelveAndHonoursPageAndSize()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        for (var index = 0; index < 5; index++)
        {
            await SeedAsync(factory, tenant, $"Course {index}", "beginner", published: [100], at: Start.AddMinutes(index));
        }

        var defaults = await ListAsync(factory, tenant);
        var second = await ListAsync(factory, tenant, "?_page=2&_size=2");

        Assert.Equal(12, defaults.GetProperty("pagination").GetProperty("size").GetInt32());
        Assert.Equal(5, defaults.GetProperty("data").GetArrayLength());
        Assert.Equal(2, second.GetProperty("data").GetArrayLength());
        Assert.Equal(3, second.GetProperty("pagination").GetProperty("totalPages").GetInt32());
        Assert.Equal(5, second.GetProperty("pagination").GetProperty("total").GetInt32());
        Assert.Equal("Course 2", second.GetProperty("data")[0].GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(CardShowsLowestPriceAndPublishedOfferCountOnly))]
    public async Task CardShowsLowestPriceAndPublishedOfferCountOnly()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        await SeedAsync(factory, tenant, "Many offers", "intermediate", published: [49700, 29700], draft: [100]);

        var card = (await ListAsync(factory, tenant)).GetProperty("data")[0];

        Assert.Equal(29700, card.GetProperty("lowestPriceCents").GetInt32());
        Assert.Equal(2, card.GetProperty("offerCount").GetInt32());
    }

    [Fact(DisplayName = nameof(SummaryIsTheTaglineOrTheDescriptionCutAtTwoHundredCharacters))]
    public async Task SummaryIsTheTaglineOrTheDescriptionCutAtTwoHundredCharacters()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var longDescription = new string('a', 150) + new string('b', 150);
        await SeedAsync(factory, tenant, "With tagline", "beginner", published: [100], description: longDescription, tagline: "Chamada comercial", at: Start);
        await SeedAsync(factory, tenant, "Long description", "beginner", published: [100], description: longDescription, at: Start.AddMinutes(1));
        await SeedAsync(factory, tenant, "Short description", "beginner", published: [100], description: "Texto curto.", at: Start.AddMinutes(2));

        var summaries = (await ListAsync(factory, tenant)).GetProperty("data").EnumerateArray()
            .ToDictionary(card => card.GetProperty("title").GetString()!, card => card.GetProperty("summary").GetString()!);

        Assert.Equal("Chamada comercial", summaries["With tagline"]);
        Assert.Equal(longDescription[..200], summaries["Long description"]);
        Assert.Equal("Texto curto.", summaries["Short description"]);
    }

    [Fact(DisplayName = nameof(ResponseNeverCarriesPersonVideoOrInternalFieldsAndIsNotCacheable))]
    public async Task ResponseNeverCarriesPersonVideoOrInternalFieldsAndIsNotCacheable()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        await SeedAsync(factory, tenant, "Course", "beginner", published: [100]);

        using var response = await GetAsync(factory, tenant);
        var body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            ["courseId", "level", "lowestPriceCents", "offerCount", "summary", "title"],
            document.RootElement.GetProperty("data")[0].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        foreach (var forbidden in new[] { "videoId", "author", "email", "publishedBy", "tenant", "tagline", "offerId", "inShowcase" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = nameof(CourseThatLosesItsLevelLeavesTheShowcaseWithoutUnpublishingAndComesBack))]
    public async Task CourseThatLosesItsLevelLeavesTheShowcaseWithoutUnpublishingAndComesBack()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", published: [100]);
        Assert.Single((await ListAsync(factory, tenant)).GetProperty("data").EnumerateArray());

        await ApplyVersionAsync(factory, tenant, courseId, version: 2, level: null);
        Assert.Empty((await ListAsync(factory, tenant)).GetProperty("data").EnumerateArray());
        Assert.Equal(["published"], await OfferStatusesAsync(factory, tenant));

        await ApplyVersionAsync(factory, tenant, courseId, version: 3, level: "advanced");
        var back = Assert.Single((await ListAsync(factory, tenant)).GetProperty("data").EnumerateArray());
        Assert.Equal("advanced", back.GetProperty("level").GetString());
        Assert.Equal(["published"], await OfferStatusesAsync(factory, tenant));
    }

    [Fact(DisplayName = nameof(AssertionOfAnotherSchoolOnlySeesThatSchool))]
    public async Task AssertionOfAnotherSchoolOnlySeesThatSchool()
    {
        var other = Guid.CreateVersion7();
        await using var factory = new ShowcaseApiFactory(fixture, tenant, other);
        await SeedAsync(factory, tenant, "School A", "beginner", published: [100]);
        await SeedAsync(factory, other, "School B", "beginner", published: [100]);

        var forA = await ListAsync(factory, tenant);
        var forB = await ListAsync(factory, other);

        Assert.Equal("School A", Assert.Single(forA.GetProperty("data").EnumerateArray()).GetProperty("title").GetString());
        Assert.Equal("School B", Assert.Single(forB.GetProperty("data").EnumerateArray()).GetProperty("title").GetString());
    }

    [Fact(DisplayName = nameof(AssertionOfASchoolOutsideTheIssuerAllowListIsRejected))]
    public async Task AssertionOfASchoolOutsideTheIssuerAllowListIsRejected()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);

        using var response = await GetAsync(factory, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("SERVICE_ASSERTION_INVALID", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(MissingAssertionIsUnauthorized))]
    public async Task MissingAssertionIsUnauthorized()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Route, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("SERVICE_ASSERTION_INVALID", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(AssertionWithoutTheShowcaseScopeIsForbidden))]
    public async Task AssertionWithoutTheShowcaseScopeIsForbidden()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);

        using var response = await SendAsync(factory, factory.CreateAssertion(tenant, "purchase-intent:write"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("SCOPE_DENIED", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ReplayedAssertionIsUnauthorized))]
    public async Task ReplayedAssertionIsUnauthorized()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var assertion = factory.CreateAssertion(tenant);

        using var first = await SendAsync(factory, assertion);
        using var replay = await SendAsync(factory, assertion);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Contains("SERVICE_ASSERTION_INVALID", await replay.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ActorJwtOnThePublicRouteIsUnauthorized))]
    public async Task ActorJwtOnThePublicRouteIsUnauthorized()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", Guid.CreateVersion7().ToString()), new Claim("tenantId", tenant.ToString()), new Claim("permissions", "oferta.editar")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));

        using var response = await SendAsync(factory, new JwtSecurityTokenHandler().WriteToken(token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("SERVICE_ASSERTION_INVALID", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ServiceAssertionOnAnActorRouteIsUnauthorized))]
    public async Task ServiceAssertionOnAnActorRouteIsUnauthorized()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.CreateAssertion(tenant));

        using var response = await client.GetAsync("/internal/v1/catalog/courses", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("TOKEN_INVALID", await response.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(RealHostRegistersBothSchemesAndAcceptsTheAssertionSignedByTheBffFactory))]
    public async Task RealHostRegistersBothSchemesAndAcceptsTheAssertionSignedByTheBffFactory()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var schemes = (await factory.Services.GetRequiredService<IAuthenticationSchemeProvider>().GetAllSchemesAsync()).Select(scheme => scheme.Name).ToArray();

        using var response = await GetAsync(factory, tenant);

        Assert.Contains("Bearer", schemes);
        Assert.Contains("ServiceAssertion", schemes);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonElement> ListAsync(ShowcaseApiFactory factory, Guid forTenant, string query = "")
    {
        using var response = await GetAsync(factory, forTenant, query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> GetAsync(ShowcaseApiFactory factory, Guid forTenant, string query = "")
        => SendAsync(factory, factory.CreateAssertion(forTenant), query);

    private static async Task<HttpResponseMessage> SendAsync(ShowcaseApiFactory factory, string bearer, string query = "")
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Route}{query}");
        request.Headers.Authorization = new("Bearer", bearer);
        return await client.SendAsync(request, Cancellation);
    }

    private static async Task<Guid> SeedAsync(
        ShowcaseApiFactory factory,
        Guid schoolId,
        string title,
        string? level,
        int[]? published = null,
        int[]? draft = null,
        bool unpublishAll = false,
        bool offersWhileLevelPresent = false,
        string description = "Pedagogical description",
        string? tagline = null,
        DateTimeOffset? at = null)
    {
        var courseId = Guid.CreateVersion7();
        var now = at ?? Start;
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(schoolId);
        var store = scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>();
        // A course whose offers were published while it still had a level, then lost it (DP-04), starts with a level.
        var initialLevel = offersWhileLevelPresent ? "beginner" : level;
        await store.ApplyAsync(PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(
            schoolId, courseId, version: 1, rich: true, title: title, level: initialLevel, description: description)), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.Include(view => view.Offers).SingleAsync(view => view.CourseId == courseId, Cancellation);
        if (tagline is not null)
        {
            course.UpdateTagline(tagline);
        }

        foreach (var price in draft ?? [])
        {
            course.CreateOffer(new($"Draft {price}", price, AccessPeriod.Create("lifetime", null)), now);
        }

        foreach (var price in published ?? [])
        {
            var offer = course.CreateOffer(new($"Offer {price}", price, AccessPeriod.Create("months", 12)), now);
            course.PublishOffer(offer.OfferId, now);
        }

        if (unpublishAll)
        {
            foreach (var offer in course.Offers.Where(item => item.Status == "published").ToList())
            {
                course.UnpublishOffer(offer.OfferId, now);
            }
        }

        await db.SaveChangesAsync(Cancellation);
        if (offersWhileLevelPresent && level is null)
        {
            await ApplyVersionAsync(factory, schoolId, courseId, version: 2, level: null, title: title);
        }

        return courseId;
    }

    private static async Task ApplyVersionAsync(ShowcaseApiFactory factory, Guid schoolId, Guid courseId, int version, string? level, string title = "Course")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(schoolId, courseId, version, rich: true, title: title, level: level)), Cancellation);
    }

    private static async Task<string[]> OfferStatusesAsync(ShowcaseApiFactory factory, Guid schoolId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(schoolId);
        return await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOffers.Select(offer => offer.Status).ToArrayAsync(Cancellation);
    }
}
