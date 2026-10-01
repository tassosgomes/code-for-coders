using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class CatalogCourseRecordTests(CommerceIntegrationFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();

    [Fact(DisplayName = nameof(KnownCourseHasImplicitRecordAndCurrentRecommendedTitles))]
    public async Task KnownCourseHasImplicitRecordAndCurrentRecommendedTitles()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient();
        var known = Guid.CreateVersion7(); var unknown = Guid.CreateVersion7(); var foreign = Guid.CreateVersion7();
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(_tenant, known, rich: true, title: "Current title"));
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(Guid.CreateVersion7(), foreign, rich: true, title: "Private title"));
        using var document = JsonDocument.Parse(CatalogCourseFactFixture.Create(_tenant, _course, rich: true));
        var body = document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        body["prerequisite"] = new
        {
            text = "Basic C#",
            recommendedCourses = new[] {
            new { courseId = known, title = "Old title" }, new { courseId = unknown, title = "Publication title" }, new { courseId = foreign, title = "Published foreign title" } }
        };
        await ApplyAsync(factory, JsonSerializer.SerializeToUtf8Bytes(body));
        using var response = await SendAsync(factory, client);
        var record = await response.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("beginner", record!.Level);
        Assert.Equal("Basic C#", record.Prerequisite.Text); Assert.Equal(new[] { "Current title", "Publication title", "Published foreign title" }, record.Prerequisite.RecommendedCourses.Select(reference => reference.Title));
        Assert.Null(record.Tagline); Assert.False(record.InShowcase); Assert.Empty(record.Offers);
    }

    [Fact(DisplayName = nameof(LegacyRecordHasNullLevelAndEmptyPrerequisite))]
    public async Task LegacyRecordHasNullLevelAndEmptyPrerequisite()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient();
        await SeedAsync(factory, rich: false); using var response = await SendAsync(factory, client);
        var record = await response.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation);
        Assert.Null(record!.Level); Assert.Null(record.Prerequisite.Text); Assert.Empty(record.Prerequisite.RecommendedCourses);
    }

    [Fact(DisplayName = nameof(TaglinePersistsAndNullClearsWithoutOutboxMessages))]
    public async Task TaglinePersistsAndNullClearsWithoutOutboxMessages()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var saved = await SendAsync(factory, client, "{\"tagline\":\"Commercial\"}", "save"); Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var reread = await SendAsync(factory, client); Assert.Contains("Commercial", await reread.Content.ReadAsStringAsync(Cancellation));
        using var cleared = await SendAsync(factory, client, "{\"tagline\":null}", "clear");
        Assert.Null((await cleared.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation))!.Tagline);
        await using var scope = Scope(factory);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.CountAsync(Cancellation));
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().OutboxMessages.ToListAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(InvalidTaglineReturns422WithFieldAndLimit))]
    [InlineData(0)]
    [InlineData(161)]
    public async Task InvalidTaglineReturns422WithFieldAndLimit(int length)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var response = await SendAsync(factory, client, JsonSerializer.Serialize(new { tagline = new string('a', length) }), "invalid");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("FIELD_INVALID", problem); Assert.Contains("tagline", problem); Assert.Contains("160", problem);
        await using var scope = Scope(factory); Assert.Empty(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingIdempotencyKeyAndWrongFieldTypeReturn400))]
    public async Task MissingIdempotencyKeyAndWrongFieldTypeReturn400()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var missing = await SendAsync(factory, client, "{\"tagline\":\"Test\"}");
        using var invalid = await SendAsync(factory, client, "{\"tagline\":42}", "invalid");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("INVALID_REQUEST", await missing.Content.ReadAsStringAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(OtherSchoolAndMissingCourseAreIndistinguishable))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OtherSchoolAndMissingCourseAreIndistinguishable(bool edit)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient();
        await ApplyAsync(factory, CatalogCourseFactFixture.Create(Guid.CreateVersion7(), _course, rich: true));
        using var other = await SendAsync(factory, client, edit ? "{\"tagline\":null}" : null, "other");
        using var missing = await SendAsync(factory, client, edit ? "{\"tagline\":null}" : null, "missing", courseId: Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode); Assert.Equal(other.StatusCode, missing.StatusCode);
        Assert.Contains("CATALOG_COURSE_NOT_FOUND", await other.Content.ReadAsStringAsync(Cancellation));
        Assert.Contains("CATALOG_COURSE_NOT_FOUND", await missing.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ReplayReturnsStoredResponseAndDoesNotReapplyAfterAnotherEdit))]
    public async Task ReplayReturnsStoredResponseAndDoesNotReapplyAfterAnotherEdit()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var first = await SendAsync(factory, client, "{\"tagline\":\"First\"}", "intent");
        using var second = await SendAsync(factory, client, "{\"tagline\":\"Second\"}", "next");
        using var replay = await SendAsync(factory, client, "{ \"tagline\" : \"First\" }", "intent");
        Assert.Equal(await first.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        using var current = await SendAsync(factory, client); Assert.Equal("Second", (await current.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation))!.Tagline);
        await using var scope = Scope(factory); Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ChangedBodyAndChangedCourseWithSameKeyReturn422))]
    public async Task ChangedBodyAndChangedCourseWithSameKeyReturn422()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var other = Guid.CreateVersion7(); await ApplyAsync(factory, CatalogCourseFactFixture.Create(_tenant, other, rich: true));
        using var first = await SendAsync(factory, client, "{\"tagline\":\"First\"}", "intent");
        using var changed = await SendAsync(factory, client, "{\"tagline\":\"Second\"}", "intent");
        using var changedCourse = await SendAsync(factory, client, "{\"tagline\":\"First\"}", "intent", courseId: other);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode); Assert.Equal(changed.StatusCode, changedCourse.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_REUSED", await changed.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentReplayCreatesOneReceipt))]
    public async Task ConcurrentReplayCreatesOneReceipt()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(factory, client, "{\"tagline\":\"Concurrent\"}", "same")));
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
        await using var scope = Scope(factory); Assert.Single(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ReceiptExpiresAfter24HoursAndActorsHaveSeparateKeys))]
    public async Task ReceiptExpiresAfter24HoursAndActorsHaveSeparateKeys()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var first = await SendAsync(factory, client, "{\"tagline\":\"First\"}", "intent");
        await using (var scope = Scope(factory))
        {
            var receipt = Assert.Single(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.ToListAsync(Cancellation));
            Assert.InRange(receipt.ExpiresAt, DateTimeOffset.UtcNow.AddHours(23.9), DateTimeOffset.UtcNow.AddHours(24.1));
            receipt.Store(receipt.RequestHash, receipt.ResponseJson, DateTimeOffset.UtcNow.AddHours(-25));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
        }
        using var expired = await SendAsync(factory, client, "{\"tagline\":\"Expired replacement\"}", "intent"); Assert.Equal(HttpStatusCode.OK, expired.StatusCode);
        using var actor = await SendAsync(factory, client, "{\"tagline\":\"Other actor\"}", "intent", actorId: Guid.CreateVersion7()); Assert.Equal(HttpStatusCode.OK, actor.StatusCode);
        await using var check = Scope(factory); Assert.Equal(2, await check.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogEditReceipts.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(AbsentTaglineLeavesTheValueUnchanged))]
    public async Task AbsentTaglineLeavesTheValueUnchanged()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var first = await SendAsync(factory, client, "{\"tagline\":\"Original\"}", "set");
        using var absent = await SendAsync(factory, client, "{}", "no-change"); Assert.Equal(HttpStatusCode.OK, absent.StatusCode);
        Assert.Equal("Original", (await absent.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation))!.Tagline);
    }

    [Theory(DisplayName = nameof(ReadAndWriteRequireEditOffers))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadAndWriteRequireEditOffers(bool edit)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var response = await SendAsync(factory, client, edit ? "{\"tagline\":null}" : null, "forbidden", permission: "autoria.editar");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task SeedAsync(CatalogCourseApiFactory factory, bool rich = true)
        => ApplyAsync(factory, CatalogCourseFactFixture.Create(_tenant, _course, rich: rich));

    [Theory(DisplayName = nameof(IdempotencyKeyUsesContractLengthLimit))]
    [InlineData(128, HttpStatusCode.OK)]
    [InlineData(129, HttpStatusCode.BadRequest)]
    public async Task IdempotencyKeyUsesContractLengthLimit(int length, HttpStatusCode status)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var response = await SendAsync(factory, client, "{\"tagline\":null}", new string('k', length));
        Assert.Equal(status, response.StatusCode);
    }

    private static async Task ApplyAsync(CatalogCourseApiFactory factory, byte[] body)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(PublishedCourseFact.Parse(body), Cancellation);
    }

    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant); return scope;
    }

    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, string? body = null,
        string? key = null, Guid? courseId = null, Guid? actorId = null, string permission = "oferta.editar")
    {
        var token = new JwtSecurityToken("identity", "commerce", [new Claim("sub", (actorId ?? _actor).ToString()),
            new Claim("tenantId", _tenant.ToString()), new Claim("permissions", permission)], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2),
            new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Patch, $"/internal/v1/catalog/courses/{courseId ?? _course:D}");
        request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Cancellation);
    }
}
