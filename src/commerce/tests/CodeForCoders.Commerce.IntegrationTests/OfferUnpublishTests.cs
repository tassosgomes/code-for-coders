using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Contracts;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OfferUnpublishTests(CommerceIntegrationFixture fixture)
{
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Trace = "00-708192a3b4c5d6e7f8091a2b3c4d5e6f-708192a3b4c5d6e7-01";
    private static string Path(Guid id, string action = "unpublish") => $"/internal/v1/catalog/offers/{id:D}/{action}";

    private CatalogCourseApiFactory Factory() => new(fixture)
    {
        CustomizeServices = services => services.Configure<OutboxOptions>(options => options.PollingIntervalSeconds = 60)
    };

    [Fact(DisplayName = nameof(UnpublishAndOutboxMatchApprovedContractsAndAuthor))]
    public async Task UnpublishAndOutboxMatchApprovedContractsAndAuthor()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        using var response = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "unpublish");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation))!;
        Assert.Equal("unpublished", result.Status); Assert.NotNull(result.PublishedAt);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal(3, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
        Assert.False((await db.CatalogCourseViews.SingleAsync(Cancellation)).InShowcase);
        AssertPair(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
        Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation)); Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(UnpublishingOneOfTwoKeepsTheShowcaseMomentAndLastOneClearsIt))]
    public async Task UnpublishingOneOfTwoKeepsTheShowcaseMomentAndLastOneClearsIt()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published", "published");
        var entry = await ShowcaseSinceAsync(factory);
        using var first = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "first"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(entry, await ShowcaseSinceAsync(factory)); Assert.NotNull(entry);
        Assert.True(await InShowcaseInBackofficeAsync(factory, client));
        using var last = await SendAsync(factory, client, HttpMethod.Post, Path(offers[1]), "last"); Assert.Equal(HttpStatusCode.OK, last.StatusCode);
        Assert.Null(await ShowcaseSinceAsync(factory)); Assert.False(await InShowcaseInBackofficeAsync(factory, client));
        using var ficha = await SendAsync(factory, client, HttpMethod.Get, $"/internal/v1/catalog/courses/{_course:D}", null);
        var statuses = (await ficha.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("offers").EnumerateArray()
            .Select(offer => offer.GetProperty("status").GetString()).ToArray();
        Assert.Equal(["unpublished", "unpublished"], statuses);
    }

    [Theory(DisplayName = nameof(OfferThatIsNotPublishedConflictsWithoutMessagesOrReceipt))]
    [InlineData("draft")]
    [InlineData("unpublished")]
    public async Task OfferThatIsNotPublishedConflictsWithoutMessagesOrReceipt(string status)
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, status);
        using var response = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "conflict");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.Contains("OFFER_STATE_CONFLICT", body);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var offer = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal(status, offer.Status); Assert.Equal(status == "draft" ? 1 : 3, offer.OfferRevision);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Empty(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(ConcurrentSameKeyReturnsOneResponseOneFactAndOneAct))]
    public async Task ConcurrentSameKeyReturnsOneResponseOneFactAndOneAct()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "same")));
        var json = await responses[0].Content.ReadAsStringAsync(Cancellation);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(json, await response.Content.ReadAsStringAsync(Cancellation)); response.Dispose();
        }
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        AssertPair(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation));
        Assert.Equal(3, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
    }

    [Fact(DisplayName = nameof(NewIntentConflictsAndKeyCannotTargetAnotherOffer))]
    public async Task NewIntentConflictsAndKeyCannotTargetAnotherOffer()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        using var first = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "first"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var conflict = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "second");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode); Assert.Contains("OFFER_STATE_CONFLICT", await conflict.Content.ReadAsStringAsync(Cancellation));
        using var reused = await SendAsync(factory, client, HttpMethod.Post, Path(Guid.CreateVersion7()), "first");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode); Assert.Contains("IDEMPOTENCY_KEY_REUSED", await reused.Content.ReadAsStringAsync(Cancellation));
        await using var scope = Scope(factory); Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOutboxMessages.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(UnpublishedOfferCannotBeDeletedAndCanBeRepublished))]
    public async Task UnpublishedOfferCannotBeDeletedAndCanBeRepublished()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        using var unpublished = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "unpublish"); Assert.Equal(HttpStatusCode.OK, unpublished.StatusCode);
        using var deleted = await SendAsync(factory, client, HttpMethod.Delete, $"/internal/v1/catalog/offers/{offers[0]:D}", "delete");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deleted.StatusCode); Assert.Contains("OFFER_STATE_CONFLICT", await deleted.Content.ReadAsStringAsync(Cancellation));
        using var republished = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0], "publish"), "republish");
        Assert.Equal(HttpStatusCode.OK, republished.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var offer = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal("published", offer.Status); Assert.Equal(4, offer.OfferRevision);
        Assert.True((await db.CatalogCourseViews.SingleAsync(Cancellation)).InShowcase);
        Assert.Equal(4, await db.CatalogOutboxMessages.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(FailureAfterSaveBeforeTransactionCommitRollsBackEverything))]
    public async Task FailureAfterSaveBeforeTransactionCommitRollsBackEverything()
    {
        await using var factory = Factory(); var configure = factory.CustomizeServices;
        factory.CustomizeServices = services =>
        {
            configure!(services); services.RemoveAll<IUnitOfWork>(); services.AddScoped<IUnitOfWork, FailAfterSaveUnitOfWork>();
        };
        using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        using var response = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "rollback");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var offer = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal("published", offer.Status); Assert.Equal(2, offer.OfferRevision); Assert.True((await db.CatalogCourseViews.SingleAsync(Cancellation)).InShowcase);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Empty(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MissingKeyForeignTenantAndPermissionCannotUnpublish))]
    public async Task MissingKeyForeignTenantAndPermissionCannotUnpublish()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offers = await SeedAsync(factory, "published");
        using var absent = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), null); Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        using var foreign = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "foreign", Guid.CreateVersion7()); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var missing = await SendAsync(factory, client, HttpMethod.Post, Path(Guid.CreateVersion7()), "missing"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var denied = await SendAsync(factory, client, HttpMethod.Post, Path(offers[0]), "denied", permission: "autoria.editar"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal("published", (await db.CatalogOffers.SingleAsync(Cancellation)).Status); Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
    }

    private void AssertPair(List<CatalogOutboxMessage> rows)
    {
        Assert.Equal(2, rows.Count);
        var fact = rows.Single(row => row.RoutingKey == "catalogo.oferta-despublicada.v1");
        var act = rows.Single(row => row.RoutingKey == "auditoria.ato-praticado.v1");
        using var factJson = JsonDocument.Parse(fact.Payload); using var actJson = JsonDocument.Parse(act.Payload);
        OfferPublicationContract.AssertValid(factJson.RootElement); OfferPublicationContract.AssertValid(actJson.RootElement, true);
        Assert.Equal(3, factJson.RootElement.GetProperty("offerRevision").GetInt32());
        Assert.Equal("oferta-despublicada", actJson.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(fact.Id, actJson.RootElement.GetProperty("fatoId").GetGuid());
        Assert.Equal(_actor, actJson.RootElement.GetProperty("autor").GetProperty("id").GetGuid());
        Assert.Equal(_tenant, actJson.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal(_course.ToString("D"), actJson.RootElement.GetProperty("complemento").GetProperty("curso").GetString());
        Assert.Equal(factJson.RootElement.GetProperty("occurredAt").GetDateTimeOffset(), actJson.RootElement.GetProperty("praticadoEm").GetDateTimeOffset());
        Assert.False(actJson.RootElement.TryGetProperty("motivo", out _));
        Assert.False(actJson.RootElement.TryGetProperty("priceCents", out _));
    }

    private async Task<DateTimeOffset?> ShowcaseSinceAsync(CatalogCourseApiFactory factory)
    {
        await using var scope = Scope(factory);
        return (await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogCourseViews.AsNoTracking().SingleAsync(Cancellation)).InShowcaseSince;
    }

    private async Task<bool> InShowcaseInBackofficeAsync(CatalogCourseApiFactory factory, HttpClient client)
    {
        using var list = await SendAsync(factory, client, HttpMethod.Get, "/internal/v1/catalog/courses", null);
        using var ficha = await SendAsync(factory, client, HttpMethod.Get, $"/internal/v1/catalog/courses/{_course:D}", null);
        var fromList = (await list.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("data").EnumerateArray()
            .Single(course => course.GetProperty("courseId").GetGuid() == _course).GetProperty("inShowcase").GetBoolean();
        var fromFicha = (await ficha.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("inShowcase").GetBoolean();
        Assert.Equal(fromList, fromFicha); return fromList;
    }

    private async Task<Guid[]> SeedAsync(CatalogCourseApiFactory factory, params string[] states)
    {
        await using var scope = Scope(factory);
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(_tenant, _course, rich: true, level: "beginner")), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); var course = await db.CatalogCourseViews.SingleAsync(Cancellation);
        var ids = new List<Guid>(); var now = DateTimeOffset.UtcNow;
        foreach (var (state, index) in states.Select((state, index) => (state, index)))
        {
            var offer = course.CreateOffer(new($"Option {index}", 49700 + index, AccessPeriod.Create("months", 12)), now);
            if (state != "draft") course.PublishOffer(offer.OfferId, now.AddSeconds(index));
            if (state == "unpublished") course.UnpublishOffer(offer.OfferId, now.AddSeconds(index + 1));
            ids.Add(offer.OfferId);
        }
        await db.SaveChangesAsync(Cancellation); return [.. ids];
    }

    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant); return scope;
    }

    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, HttpMethod method, string path, string? key,
        Guid? tenant = null, string permission = "oferta.editar")
    {
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", _actor.ToString()), new Claim("tenantId", (tenant ?? _tenant).ToString()), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        if (key is not null) request.Headers.Add("Idempotency-Key", key); request.Headers.Add("traceparent", Trace);
        return client.SendAsync(request, Cancellation);
    }
}
