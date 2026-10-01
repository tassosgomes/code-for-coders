using System.Net;
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
public sealed class ShowcaseCourseDetailTests(CommerceIntegrationFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid tenant = Guid.CreateVersion7();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact(DisplayName = nameof(PageCarriesTheCourseStructureWithoutVideoAndThePublishedOffersFromCheapestToDearest))]
    public async Task PageCarriesTheCourseStructureWithoutVideoAndThePublishedOffersFromCheapestToDearest()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Advanced course", "advanced", description: "Do primeiro programa a uma API.",
            prerequisiteText: "Git e C# básico.", modules: Modules(("Fundamentos", ["Tipos", "Controle de fluxo"]), ("API", ["Rotas"])),
            offers: [(89700, "lifetime", null), (39700, "months", 12), (100, "months", 1)]);

        var page = await GetPageAsync(factory, tenant, courseId);

        Assert.Equal(courseId, page.GetProperty("courseId").GetGuid());
        Assert.Equal("Advanced course", page.GetProperty("title").GetString());
        Assert.Equal("advanced", page.GetProperty("level").GetString());
        Assert.Equal("Do primeiro programa a uma API.", page.GetProperty("description").GetString());
        Assert.Equal("Git e C# básico.", page.GetProperty("prerequisite").GetProperty("text").GetString());
        Assert.Equal(
            ["Fundamentos:Tipos|Controle de fluxo", "API:Rotas"],
            page.GetProperty("modules").EnumerateArray().Select(module =>
                $"{module.GetProperty("title").GetString()}:{string.Join('|', module.GetProperty("lessons").EnumerateArray().Select(lesson => lesson.GetProperty("title").GetString()))}"));
        var offers = page.GetProperty("offers").EnumerateArray().ToArray();
        Assert.Equal([100, 39700, 89700], offers.Select(offer => offer.GetProperty("priceCents").GetInt32()));
        Assert.Equal(["months", "months", "lifetime"], offers.Select(offer => offer.GetProperty("accessPeriod").GetProperty("type").GetString()));
        Assert.Equal(1, offers[0].GetProperty("accessPeriod").GetProperty("months").GetInt32());
        Assert.False(offers[2].GetProperty("accessPeriod").TryGetProperty("months", out _));
        Assert.Equal(
            ["accessPeriod", "name", "offerId", "priceCents"],
            offers[0].EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact(DisplayName = nameof(OnlyPublishedOffersAppearAndAnEmptyDescriptionStaysEmpty))]
    public async Task OnlyPublishedOffersAppearAndAnEmptyDescriptionStaysEmpty()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", description: "",
            offers: [(30000, "lifetime", null)], draftPrices: [100], unpublishedPrices: [200]);

        var page = await GetPageAsync(factory, tenant, courseId);

        Assert.Equal("", page.GetProperty("description").GetString());
        Assert.Equal([30000], page.GetProperty("offers").EnumerateArray().Select(offer => offer.GetProperty("priceCents").GetInt32()));
        Assert.Null(page.GetProperty("prerequisite").GetProperty("text").GetString());
        Assert.Empty(page.GetProperty("prerequisite").GetProperty("recommendedCourses").EnumerateArray());
    }

    [Fact(DisplayName = nameof(RecommendedCourseInTheShowcaseIsFlaggedAndShownWithItsCurrentTitle))]
    public async Task RecommendedCourseInTheShowcaseIsFlaggedAndShownWithItsCurrentTitle()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var recommended = await SeedAsync(factory, tenant, "Fundamentos de C# (novo título)", "beginner", offers: [(29700, "lifetime", null)]);
        var courseId = await SeedAsync(factory, tenant, "Course", "advanced", offers: [(39700, "lifetime", null)],
            recommended: [(recommended, "Fundamentos de C# (título antigo)")]);

        var page = await GetPageAsync(factory, tenant, courseId);

        var reference = Assert.Single(page.GetProperty("prerequisite").GetProperty("recommendedCourses").EnumerateArray());
        Assert.Equal(recommended, reference.GetProperty("courseId").GetGuid());
        Assert.Equal("Fundamentos de C# (novo título)", reference.GetProperty("title").GetString());
        Assert.True(reference.GetProperty("inShowcase").GetBoolean());
    }

    [Fact(DisplayName = nameof(RecommendedCourseOutsideTheShowcaseIsFlaggedFalseWithCurrentOrPublicationTitle))]
    public async Task RecommendedCourseOutsideTheShowcaseIsFlaggedFalseWithCurrentOrPublicationTitle()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var noOffer = await SeedAsync(factory, tenant, "Introdução a APIs (atual)", "beginner");
        var unknown = Guid.CreateVersion7();
        var courseId = await SeedAsync(factory, tenant, "Course", "advanced", offers: [(39700, "lifetime", null)],
            recommended: [(noOffer, "Introdução a APIs (publicação)"), (unknown, "Curso que o Catálogo desconhece")]);

        var page = await GetPageAsync(factory, tenant, courseId);

        var references = page.GetProperty("prerequisite").GetProperty("recommendedCourses").EnumerateArray().ToArray();
        Assert.Equal(["Introdução a APIs (atual)", "Curso que o Catálogo desconhece"], references.Select(reference => reference.GetProperty("title").GetString()));
        Assert.All(references, reference => Assert.False(reference.GetProperty("inShowcase").GetBoolean()));
    }

    [Fact(DisplayName = nameof(UnknownOutsideTheShowcaseAndAnotherSchoolsCourseGetIdenticalNotFoundResponses))]
    public async Task UnknownOutsideTheShowcaseAndAnotherSchoolsCourseGetIdenticalNotFoundResponses()
    {
        var other = Guid.CreateVersion7();
        await using var factory = new ShowcaseApiFactory(fixture, tenant, other);
        var outside = await SeedAsync(factory, tenant, "No offer", "beginner");
        var noLevel = await SeedAsync(factory, tenant, "No level", null, offers: [(100, "lifetime", null)], offersWhileLevelPresent: true);
        var ofOther = await SeedAsync(factory, other, "Other school", "beginner", offers: [(100, "lifetime", null)]);

        var responses = new List<(HttpStatusCode Status, string Body, string? CacheControl)>();
        foreach (var courseId in new[] { Guid.CreateVersion7(), outside, noLevel, ofOther })
        {
            using var response = await GetAsync(factory, tenant, courseId);
            responses.Add((response.StatusCode, Normalize(await response.Content.ReadAsStringAsync(Cancellation), courseId), response.Headers.CacheControl?.ToString()));
        }

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.Status));
        Assert.All(responses, response => Assert.Contains("no-store", response.CacheControl, StringComparison.Ordinal));
        Assert.Single(responses.Select(response => response.Body).Distinct());
        Assert.Contains("SHOWCASE_COURSE_NOT_FOUND", responses[0].Body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ResponseNeverCarriesPersonVideoOrInternalFieldsAndIsNotCacheable))]
    public async Task ResponseNeverCarriesPersonVideoOrInternalFieldsAndIsNotCacheable()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", offers: [(100, "lifetime", null)], tagline: "Chamada comercial");

        using var response = await GetAsync(factory, tenant, courseId);
        var body = await response.Content.ReadAsStringAsync(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            ["courseId", "description", "level", "modules", "offers", "prerequisite", "title"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        foreach (var forbidden in new[] { "videoId", "lessonId", "moduleId", "author", "email", "publishedBy", "tenant", "tagline", "currency", "inShowcaseSince" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = nameof(RepublishedCourseWithANewLessonShowsItWithoutAnyActionFromFinance))]
    public async Task RepublishedCourseWithANewLessonShowsItWithoutAnyActionFromFinance()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", offers: [(100, "lifetime", null)],
            modules: Modules(("Módulo", ["Aula 1"])));
        Assert.Equal(1, LessonCount(await GetPageAsync(factory, tenant, courseId)));

        await ApplyVersionAsync(factory, tenant, courseId, version: 2, "beginner", Modules(("Módulo", ["Aula 1", "Aula nova"])), "Course com aula nova");

        var page = await GetPageAsync(factory, tenant, courseId);
        Assert.Equal(2, LessonCount(page));
        Assert.Equal("Course com aula nova", page.GetProperty("title").GetString());
        Assert.Contains("Aula nova", page.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(CourseThatLosesItsLevelOrItsLastPublishedOfferLeavesThePageAndComesBack))]
    public async Task CourseThatLosesItsLevelOrItsLastPublishedOfferLeavesThePageAndComesBack()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", offers: [(100, "lifetime", null)]);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, tenant, courseId));

        await ApplyVersionAsync(factory, tenant, courseId, version: 2, null, Modules(("Módulo", ["Aula"])), "Course");
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(factory, tenant, courseId));

        await ApplyVersionAsync(factory, tenant, courseId, version: 3, "advanced", Modules(("Módulo", ["Aula"])), "Course");
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, tenant, courseId));

        await UnpublishAllAsync(factory, tenant, courseId);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(factory, tenant, courseId));
    }

    [Fact(DisplayName = nameof(AssertionOfAnotherSchoolOnlyReadsThatSchoolsPage))]
    public async Task AssertionOfAnotherSchoolOnlyReadsThatSchoolsPage()
    {
        var other = Guid.CreateVersion7();
        await using var factory = new ShowcaseApiFactory(fixture, tenant, other);
        var courseOfA = await SeedAsync(factory, tenant, "School A", "beginner", offers: [(100, "lifetime", null)]);

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(factory, tenant, courseOfA));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(factory, other, courseOfA));
    }

    [Fact(DisplayName = nameof(MissingAssertionIsUnauthorizedAndWrongScopeIsForbidden))]
    public async Task MissingAssertionIsUnauthorizedAndWrongScopeIsForbidden()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", offers: [(100, "lifetime", null)]);
        using var anonymous = factory.CreateClient();

        using var missing = await anonymous.GetAsync(Route(courseId), Cancellation);
        using var wrongScope = await SendAsync(factory, courseId, factory.CreateAssertion(tenant, "purchase-intent:write"));

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Contains("SERVICE_ASSERTION_INVALID", await missing.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);
        Assert.Contains("SCOPE_DENIED", await wrongScope.Content.ReadAsStringAsync(Cancellation), StringComparison.Ordinal);
    }

    [Fact(DisplayName = nameof(ReplayedAssertionIsUnauthorized))]
    public async Task ReplayedAssertionIsUnauthorized()
    {
        await using var factory = new ShowcaseApiFactory(fixture, tenant);
        var courseId = await SeedAsync(factory, tenant, "Course", "beginner", offers: [(100, "lifetime", null)]);
        var assertion = factory.CreateAssertion(tenant);

        using var first = await SendAsync(factory, courseId, assertion);
        using var replay = await SendAsync(factory, courseId, assertion);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    private static string Route(Guid courseId) => $"/internal/v1/showcase/courses/{courseId}";

    // Only the trace identifier and the echoed path may differ between not-found cases.
    private static string Normalize(string body, Guid courseId)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body)!;
        fields.Remove("traceId");
        fields.Remove("instance");
        return JsonSerializer.Serialize(fields.OrderBy(field => field.Key, StringComparer.Ordinal).ToDictionary()).Replace(courseId.ToString(), "{id}", StringComparison.Ordinal);
    }

    private static int LessonCount(JsonElement page)
        => page.GetProperty("modules").EnumerateArray().Sum(module => module.GetProperty("lessons").GetArrayLength());

    private static async Task<JsonElement> GetPageAsync(ShowcaseApiFactory factory, Guid forTenant, Guid courseId)
    {
        using var response = await GetAsync(factory, forTenant, courseId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation)).RootElement.Clone();
    }

    private static async Task<HttpStatusCode> StatusAsync(ShowcaseApiFactory factory, Guid forTenant, Guid courseId)
    {
        using var response = await GetAsync(factory, forTenant, courseId);
        return response.StatusCode;
    }

    private static Task<HttpResponseMessage> GetAsync(ShowcaseApiFactory factory, Guid forTenant, Guid courseId)
        => SendAsync(factory, courseId, factory.CreateAssertion(forTenant));

    private static async Task<HttpResponseMessage> SendAsync(ShowcaseApiFactory factory, Guid courseId, string bearer)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Route(courseId));
        request.Headers.Authorization = new("Bearer", bearer);
        return await client.SendAsync(request, Cancellation);
    }

    private static object[] Modules(params (string Title, string[] Lessons)[] modules)
        => modules.Select((module, index) => (object)new
        {
            moduleId = Guid.CreateVersion7(),
            title = module.Title,
            position = index + 1,
            lessons = module.Lessons.Select((lesson, position) => new { lessonId = Guid.CreateVersion7(), title = lesson, position = position + 1, videoId = Guid.CreateVersion7() }).ToArray(),
        }).ToArray();

    private static async Task<Guid> SeedAsync(
        ShowcaseApiFactory factory,
        Guid schoolId,
        string title,
        string? level,
        string description = "Pedagogical description",
        string? prerequisiteText = null,
        (Guid CourseId, string Title)[]? recommended = null,
        object[]? modules = null,
        (int Price, string Type, int? Months)[]? offers = null,
        int[]? draftPrices = null,
        int[]? unpublishedPrices = null,
        bool offersWhileLevelPresent = false,
        string? tagline = null)
    {
        var courseId = Guid.CreateVersion7();
        var prerequisite = new
        {
            text = prerequisiteText,
            recommendedCourses = (recommended ?? []).Select(reference => new { courseId = reference.CourseId, title = reference.Title }).ToArray(),
        };
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(schoolId);
        var store = scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>();
        await store.ApplyAsync(PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(
            schoolId, courseId, version: 1, rich: true, title: title, level: offersWhileLevelPresent ? "beginner" : level,
            description: description, prerequisite: prerequisite, modules: modules)), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.Include(view => view.Offers).SingleAsync(view => view.CourseId == courseId, Cancellation);
        if (tagline is not null)
        {
            course.UpdateTagline(tagline);
        }

        foreach (var price in draftPrices ?? [])
        {
            course.CreateOffer(new($"Draft {price}", price, AccessPeriod.Create("lifetime", null)), Start);
        }

        foreach (var (price, type, months) in offers ?? [])
        {
            var offer = course.CreateOffer(new($"Offer {price}", price, AccessPeriod.Create(type, months)), Start);
            course.PublishOffer(offer.OfferId, Start);
        }

        foreach (var price in unpublishedPrices ?? [])
        {
            var offer = course.CreateOffer(new($"Unpublished {price}", price, AccessPeriod.Create("lifetime", null)), Start);
            course.PublishOffer(offer.OfferId, Start);
            course.UnpublishOffer(offer.OfferId, Start);
        }

        await db.SaveChangesAsync(Cancellation);
        if (offersWhileLevelPresent && level is null)
        {
            await ApplyVersionAsync(factory, schoolId, courseId, version: 2, null, modules ?? Modules(("Module", ["Lesson"])), title);
        }

        return courseId;
    }

    private static async Task ApplyVersionAsync(ShowcaseApiFactory factory, Guid schoolId, Guid courseId, int version, string? level, object[] modules, string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(schoolId, courseId, version, rich: true, title: title, level: level, modules: modules)), Cancellation);
    }

    private static async Task UnpublishAllAsync(ShowcaseApiFactory factory, Guid schoolId, Guid courseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(schoolId);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.Include(view => view.Offers).SingleAsync(view => view.CourseId == courseId, Cancellation);
        foreach (var offer in course.Offers.Where(item => item.Status == "published").ToList())
        {
            course.UnpublishOffer(offer.OfferId, Start);
        }

        await db.SaveChangesAsync(Cancellation);
    }
}
