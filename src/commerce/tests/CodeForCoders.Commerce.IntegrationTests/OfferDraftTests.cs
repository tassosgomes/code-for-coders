using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OfferDraftTests(CommerceIntegrationFixture fixture)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();
    private string CreatePath => $"/internal/v1/catalog/courses/{_course:D}/offers";
    private static string OfferPath(Guid id) => $"/internal/v1/catalog/offers/{id:D}";
    private static object Terms(int price = 49700, int months = 12) => new { name = "Acesso por período", priceCents = price, accessPeriod = new { type = "months", months } };

    [Fact(DisplayName = nameof(TwoPromiseFormsPersistReadAndDeleteWithoutOutbox))]
    public async Task TwoPromiseFormsPersistReadAndDeleteWithoutOutbox()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var first = await CreateAsync(factory, client, Terms(), "months");
        var second = await CreateAsync(factory, client, new { name = "Acesso vitalício", priceCents = 89700, accessPeriod = new { type = "lifetime" } }, "lifetime");
        Assert.Equal("draft", first.Status); Assert.Null(first.PublishedAt); Assert.Equal("lifetime", second.AccessPeriod.Type);
        using var read = await SendAsync(factory, client, HttpMethod.Get, $"/internal/v1/catalog/courses/{_course:D}");
        var record = await read.Content.ReadFromJsonAsync<CatalogCourseDetail>(Cancellation); Assert.Equal(2, record!.Offers.Count);
        using var list = await SendAsync(factory, client, HttpMethod.Get, "/internal/v1/catalog/courses");
        Assert.Contains("\"draft\":2", await list.Content.ReadAsStringAsync(Cancellation));
        await using (var scope = Scope(factory))
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var stored = await db.CatalogOffers.SingleAsync(offer => offer.OfferId == second.OfferId, Cancellation);
            Assert.Equal(89700, stored.PriceCents); Assert.Equal(AccessPeriod.Create("lifetime", null), stored.AccessPeriod);
            Assert.Equal(1, stored.OfferRevision);
            using var command = db.Database.GetDbConnection().CreateCommand(); await db.Database.OpenConnectionAsync(Cancellation);
            command.CommandText = $"SELECT access_period::text FROM catalog.offers WHERE offer_id = '{second.OfferId:D}'";
            using var period = JsonDocument.Parse((string)(await command.ExecuteScalarAsync(Cancellation))!);
            Assert.Equal("lifetime", period.RootElement.GetProperty("type").GetString()); Assert.Single(period.RootElement.EnumerateObject());
        }
        using var deleted = await SendAsync(factory, client, HttpMethod.Delete, OfferPath(first.OfferId), key: "delete");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var replay = await SendAsync(factory, client, HttpMethod.Delete, OfferPath(first.OfferId), key: "delete");
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        using var missing = await SendAsync(factory, client, HttpMethod.Delete, OfferPath(first.OfferId), key: "another"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using var check = Scope(factory); var data = check.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Single(await data.CatalogOffers.ToListAsync(Cancellation)); Assert.Empty(await data.OutboxMessages.ToListAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(InclusiveLimitsAreAccepted))]
    [InlineData(1, 1)]
    [InlineData(9999999, 60)]
    public async Task InclusiveLimitsAreAccepted(int price, int months)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var offer = await CreateAsync(factory, client, Terms(price, months), "limit"); Assert.Equal(price, offer.PriceCents); Assert.Equal(months, offer.AccessPeriod.Months);
    }

    [Theory(DisplayName = nameof(InvalidContentReturns422AndDoesNotPersist))]
    [InlineData(0, 12, "priceCents")]
    [InlineData(-1, 12, "priceCents")]
    [InlineData(10000000, 12, "priceCents")]
    [InlineData(49700, 0, "accessPeriod")]
    [InlineData(49700, 61, "accessPeriod")]
    [InlineData(49700, 1.5, "accessPeriod")]
    [InlineData(49700.5, 12, "priceCents")]
    public async Task InvalidContentReturns422AndDoesNotPersist(decimal price, decimal months, string field)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var response = await SendAsync(factory, client, HttpMethod.Post, CreatePath, new { name = "Option", priceCents = price, accessPeriod = new { type = "months", months } }, "invalid");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); var error = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("FIELD_INVALID", error); Assert.Contains(field, error);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Empty(await db.CatalogOffers.ToListAsync(Cancellation)); Assert.Empty(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentSameIntentCreatesOneOfferAndCanonicalReplayIsStable))]
    public async Task ConcurrentSameIntentCreatesOneOfferAndCanonicalReplayIsStable()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms(), "same")));
        var body = await responses[0].Content.ReadAsStringAsync(Cancellation);
        foreach (var response in responses) { Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal(body, await response.Content.ReadAsStringAsync(Cancellation)); response.Dispose(); }
        using var canonical = await SendAsync(factory, client, HttpMethod.Post, CreatePath,
            JsonSerializer.Deserialize<JsonElement>("{\"accessPeriod\":{\"months\":12,\"type\":\"months\"},\"priceCents\":49700,\"name\":\"Acesso por período\"}"), "same");
        Assert.Equal(body, await canonical.Content.ReadAsStringAsync(Cancellation));
        using var conflict = await SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms(39700), "same");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode); Assert.Contains("IDEMPOTENCY_KEY_REUSED", await conflict.Content.ReadAsStringAsync(Cancellation));
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Single(await db.CatalogOffers.ToListAsync(Cancellation)); Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation)); Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(PartialAndIdenticalUpdatesPreserveValuesAndRevision))]
    public async Task PartialAndIdenticalUpdatesPreserveValuesAndRevision()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var offer = await CreateAsync(factory, client, Terms(), "create");
        using var identical = await SendAsync(factory, client, HttpMethod.Patch, OfferPath(offer.OfferId), Terms(), "identical");
        Assert.Equal(HttpStatusCode.OK, identical.StatusCode); Assert.InRange((offer.UpdatedAt - (await identical.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation))!.UpdatedAt).Duration(), TimeSpan.Zero, TimeSpan.FromMicroseconds(1));
        await using (var scope = Scope(factory)) Assert.Equal(1, (await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
        using var changed = await SendAsync(factory, client, HttpMethod.Patch, OfferPath(offer.OfferId), new { priceCents = 89700, accessPeriod = new { type = "lifetime" } }, "change");
        var updated = await changed.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation); Assert.Equal(offer.Name, updated!.Name); Assert.Equal(89700, updated.PriceCents);
        using var replay = await SendAsync(factory, client, HttpMethod.Patch, OfferPath(offer.OfferId), new { priceCents = 89700, accessPeriod = new { type = "lifetime" } }, "change");
        Assert.Equal(await changed.Content.ReadAsStringAsync(Cancellation), await replay.Content.ReadAsStringAsync(Cancellation));
        using var invalid = await SendAsync(factory, client, HttpMethod.Patch, OfferPath(offer.OfferId), new { name = "Renamed", priceCents = 0 }, "bad-edit"); Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        await using var check = Scope(factory); var db = check.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var stored = await db.CatalogOffers.SingleAsync(Cancellation); Assert.Equal(2, stored.OfferRevision); Assert.Equal(offer.Name, stored.Name); Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(NonDraftDeletionReturnsStateConflict))]
    [InlineData("published")]
    [InlineData("unpublished")]
    public async Task NonDraftDeletionReturnsStateConflict(string status)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var offer = await CreateAsync(factory, client, Terms(), "create");
        await using (var scope = Scope(factory)) await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = {status} WHERE offer_id = {offer.OfferId}", Cancellation);
        using var response = await SendAsync(factory, client, HttpMethod.Delete, OfferPath(offer.OfferId), key: "delete"); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("OFFER_STATE_CONFLICT", await response.Content.ReadAsStringAsync(Cancellation));
        await using var check = Scope(factory); var db = check.ServiceProvider.GetRequiredService<CommerceDbContext>(); Assert.Single(await db.CatalogOffers.ToListAsync(Cancellation)); Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentCreationCannotExceedFiftyOffers))]
    public async Task ConcurrentCreationCannotExceedFiftyOffers()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        await using (var scope = Scope(factory))
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); var course = await db.CatalogCourseViews.SingleAsync(Cancellation);
            for (var index = 0; index < 49; index++) course.CreateOffer(new("Option", 1, AccessPeriod.Create("lifetime", null)), DateTimeOffset.UtcNow);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(Cancellation);
        }
        var responses = await Task.WhenAll(SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms(), "a"), SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms(), "b"));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var rejected = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.UnprocessableEntity);
        Assert.Contains("offers", await rejected.Content.ReadAsStringAsync(Cancellation)); Assert.Contains("FIELD_INVALID", await rejected.Content.ReadAsStringAsync(Cancellation));
        foreach (var response in responses) response.Dispose();
        await using var check = Scope(factory); Assert.Equal(50, await check.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOffers.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(TenantIsolationAndUnknownResourcesReturn404))]
    public async Task TenantIsolationAndUnknownResourcesReturn404()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        var offer = await CreateAsync(factory, client, Terms(), "create");
        foreach (var method in new[] { HttpMethod.Patch, HttpMethod.Delete })
        {
            using var foreign = await SendAsync(factory, client, method, OfferPath(offer.OfferId), method == HttpMethod.Patch ? new { name = "Private" } : null, "foreign", Guid.CreateVersion7());
            using var missing = await SendAsync(factory, client, method, OfferPath(Guid.CreateVersion7()), method == HttpMethod.Patch ? new { name = "Missing" } : null, "missing");
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode); Assert.Equal(missing.StatusCode, foreign.StatusCode); Assert.Contains("OFFER_NOT_FOUND", await foreign.Content.ReadAsStringAsync(Cancellation));
        }
        using var unknown = await SendAsync(factory, client, HttpMethod.Post, $"/internal/v1/catalog/courses/{Guid.CreateVersion7():D}/offers", Terms(), "unknown");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode); Assert.Contains("CATALOG_COURSE_NOT_FOUND", await unknown.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingKeysInvalidShapeAndForbiddenWritesNeverPersist))]
    public async Task MissingKeysInvalidShapeAndForbiddenWritesNeverPersist()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); await SeedAsync(factory);
        using var key = await SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms()); Assert.Equal(HttpStatusCode.BadRequest, key.StatusCode);
        using var shape = await SendAsync(factory, client, HttpMethod.Post, CreatePath, new { name = "Option", priceCents = "49700" }, "shape"); Assert.Equal(HttpStatusCode.BadRequest, shape.StatusCode);
        using var permission = await SendAsync(factory, client, HttpMethod.Post, CreatePath, Terms(), "forbidden", permission: "autoria.editar"); Assert.Equal(HttpStatusCode.Forbidden, permission.StatusCode);
        await using var check = Scope(factory); Assert.Empty(await check.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOffers.ToListAsync(Cancellation));
    }

    private async Task<CatalogOfferDetail> CreateAsync(CatalogCourseApiFactory factory, HttpClient client, object body, string key)
    {
        using var response = await SendAsync(factory, client, HttpMethod.Post, CreatePath, body, key); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var offer = (await response.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation))!;
        Assert.Equal($"/api/v1/catalog/offers/{offer.OfferId:D}", response.Headers.Location!.OriginalString); return offer;
    }
    private async Task SeedAsync(CatalogCourseApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(_tenant, _course)), Cancellation);
    }
    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant); return scope;
    }
    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, HttpMethod method, string path,
        object? body = null, string? key = null, Guid? tenant = null, string permission = "oferta.editar")
    {
        var token = new JwtSecurityToken("identity", "commerce", [new Claim("sub", _actor.ToString()), new Claim("tenantId", (tenant ?? _tenant).ToString()), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        if (body is not null) request.Content = JsonContent.Create(body); if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request, Cancellation);
    }
}
