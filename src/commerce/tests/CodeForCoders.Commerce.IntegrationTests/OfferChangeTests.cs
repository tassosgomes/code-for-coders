using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using RabbitMQ.Client;
using Npgsql;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OfferChangeTests(CommerceIntegrationFixture fixture)
{
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Trace = "00-708192a3b4c5d6e7f8091a2b3c4d5e6f-708192a3b4c5d6e7-01";

    [Theory(DisplayName = nameof(ChangedPairsMatchContractsAndShareOneFact))]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ChangedPairsMatchContractsAndShareOneFact(bool price, bool period)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient();
        var offer = await SeedAsync(factory);
        var body = new
        {
            name = "Updated option",
            priceCents = price ? 39700 : 49700,
            accessPeriod = period ? new AccessPeriod("lifetime", null) : new AccessPeriod("months", 12)
        };
        using var response = await SendAsync(factory, client, offer.OfferId, body, "change");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation))!;
        Assert.Equal(body.priceCents, result.PriceCents); Assert.Equal(body.accessPeriod, result.AccessPeriod);
        Assert.Equal("published", result.Status); Assert.Equal(offer.PublishedAt, result.PublishedAt);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal(3, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
        AssertPair(await db.CatalogOutboxMessages.ToListAsync(Cancellation), price, period);
        Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation)); Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(NameOnlySavesWithoutOutboxMessages))]
    public async Task NameOnlySavesWithoutOutboxMessages()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId, new { name = "Renamed" }, "rename");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var stored = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal("Renamed", stored.Name); Assert.Equal(3, stored.OfferRevision);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(IdenticalValuesPreserveStoredOfferAndEmitNothing))]
    public async Task IdenticalValuesPreserveStoredOfferAndEmitNothing()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId,
            new { offer.Name, offer.PriceCents, offer.AccessPeriod }, "identical");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var stored = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal(2, stored.OfferRevision); Assert.Equal(offer.UpdatedAt, stored.UpdatedAt);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
    }

    [Theory(DisplayName = nameof(DraftAndUnpublishedEditsAreSilent))]
    [InlineData("draft")]
    [InlineData("unpublished")]
    public async Task DraftAndUnpublishedEditsAreSilent(string status)
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory, status);
        using var response = await SendAsync(factory, client, offer.OfferId,
            new { priceCents = 39700, accessPeriod = new { type = "lifetime" } }, "silent");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var stored = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal(39700, stored.PriceCents); Assert.Equal("lifetime", stored.AccessPeriod.Type); Assert.Equal(status, stored.Status);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(MonthDurationChangeProducesOnlyPeriodPair))]
    public async Task MonthDurationChangeProducesOnlyPeriodPair()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId, new { accessPeriod = new { type = "months", months = 6 } }, "duration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var rows = await db.CatalogOutboxMessages.ToListAsync(Cancellation);
        AssertPair(rows, false, true, "6m");
    }

    [Fact(DisplayName = nameof(ConcurrentReplayEmitsOnePairAndConflictingBodyIsRejected))]
    public async Task ConcurrentReplayEmitsOnePairAndConflictingBodyIsRejected()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(factory, client, offer.OfferId, new { priceCents = 39700 }, "same")));
        var json = await responses[0].Content.ReadAsStringAsync(Cancellation);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(json, await response.Content.ReadAsStringAsync(Cancellation)); response.Dispose();
        }
        using var conflict = await SendAsync(factory, client, offer.OfferId, new { priceCents = 89700 }, "same");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_REUSED", await conflict.Content.ReadAsStringAsync(Cancellation));
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        AssertPair(await db.CatalogOutboxMessages.ToListAsync(Cancellation), true, false);
        Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation)); Assert.Equal(3, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
    }

    [Fact(DisplayName = nameof(FailureAfterSaveRollsBackOfferReceiptFactAndAct))]
    public async Task FailureAfterSaveRollsBackOfferReceiptFactAndAct()
    {
        await using var factory = new CatalogCourseApiFactory(fixture)
        {
            CustomizeServices = services => { services.RemoveAll<IUnitOfWork>(); services.AddScoped<IUnitOfWork, FailAfterSaveUnitOfWork>(); }
        };
        using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId, new { priceCents = 39700 }, "rollback");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertUnchangedAsync(factory);
    }

    [Fact(DisplayName = nameof(LimitsTenantPermissionAndMissingKeyNeverChangePublishedOffer))]
    public async Task LimitsTenantPermissionAndMissingKeyNeverChangePublishedOffer()
    {
        await using var factory = new CatalogCourseApiFactory(fixture); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        foreach (var price in new[] { 0m, 10000000m, 49700.5m })
        {
            using var response = await SendAsync(factory, client, offer.OfferId, new { priceCents = price }, $"invalid-{price}");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        }
        using var period = await SendAsync(factory, client, offer.OfferId, new { accessPeriod = new { type = "months", months = 61 } }, "period");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, period.StatusCode);
        using var foreign = await SendAsync(factory, client, offer.OfferId, new { priceCents = 39700 }, "foreign", Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var forbidden = await SendAsync(factory, client, offer.OfferId, new { priceCents = 39700 }, "denied", permission: "autoria.editar");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var missing = await SendAsync(factory, client, offer.OfferId, new { priceCents = 39700 }, null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        await AssertUnchangedAsync(factory);
    }

    [Fact(DisplayName = nameof(RealPublisherDeliversChangedFactAndActMatchingSchemas))]
    public async Task RealPublisherDeliversChangedFactAndActMatchingSchemas()
    {
        await using var factory = await BrokerFactoryAsync(); using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        var auditQueue = $"offer-change-audit-{_tenant:D}";
        await channel.QueueDeclareAsync(auditQueue, false, true, true, cancellationToken: Cancellation);
        await channel.QueueBindAsync(auditQueue, $"audit-{_tenant:D}", "auditoria.ato-praticado.v1", cancellationToken: Cancellation);
        var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId,
            new { priceCents = 39700, accessPeriod = new { type = "lifetime" } }, "broker");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fact = await GetAsync(channel, $"retention-{_tenant:D}"); var act = await GetAsync(channel, auditQueue);
        Assert.Equal("catalogo.oferta-alterada.v1", fact.RoutingKey); Assert.Equal($"commerce-{_tenant:D}", fact.Exchange);
        Assert.Equal($"audit-{_tenant:D}", act.Exchange);
        using var factJson = JsonDocument.Parse(fact.Body); using var actJson = JsonDocument.Parse(act.Body);
        AssertPayloadPair(factJson.RootElement, actJson.RootElement, true, true);
        var headers = act.BasicProperties.Headers!;
        var correlation = Encoding.UTF8.GetString((byte[])headers["correlationId"]!);
        Assert.StartsWith(Trace[..35], correlation); Assert.Equal(correlation, Encoding.UTF8.GetString((byte[])headers["traceparent"]!));
        await WaitAsync(async () =>
        {
            await using var scope = Scope(factory);
            return await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOutboxMessages.AllAsync(row => row.ProcessedOn != null, Cancellation);
        });
    }

    private void AssertPair(List<CatalogOutboxMessage> rows, bool price, bool period, string newPeriod = "vitalicia")
    {
        Assert.Equal(2, rows.Count);
        var fact = rows.Single(row => row.RoutingKey == "catalogo.oferta-alterada.v1");
        var act = rows.Single(row => row.RoutingKey == "auditoria.ato-praticado.v1");
        using var factJson = JsonDocument.Parse(fact.Payload); using var actJson = JsonDocument.Parse(act.Payload);
        Assert.Equal(fact.Id, factJson.RootElement.GetProperty("eventId").GetGuid());
        AssertPayloadPair(factJson.RootElement, actJson.RootElement, price, period, newPeriod);
    }

    private void AssertPayloadPair(JsonElement fact, JsonElement act, bool price, bool period, string newPeriod = "vitalicia")
    {
        CommerceMessages.AssertSends("catalogo.oferta-alterada.v1", fact); CommerceMessages.AssertSends("auditoria.ato-praticado.v1", act);
        Assert.Equal(fact.GetProperty("eventId").GetGuid(), act.GetProperty("fatoId").GetGuid());
        Assert.Equal("oferta-alterada", act.GetProperty("tipo").GetString()); Assert.Equal("catalogo", act.GetProperty("origem").GetString());
        Assert.Equal(_actor, act.GetProperty("autor").GetProperty("id").GetGuid()); Assert.Equal(_tenant, act.GetProperty("tenantId").GetGuid());
        Assert.Equal(fact.GetProperty("offerId").GetGuid(), act.GetProperty("alvo").GetProperty("id").GetGuid());
        Assert.Equal(fact.GetProperty("occurredAt").GetDateTimeOffset(), act.GetProperty("praticadoEm").GetDateTimeOffset());
        var previous = fact.GetProperty("previous"); Assert.Equal(49700, previous.GetProperty("priceCents").GetInt32());
        Assert.Equal(12, previous.GetProperty("accessPeriod").GetProperty("months").GetInt32());
        var complement = act.GetProperty("complemento"); Assert.Equal(1 + (price ? 2 : 0) + (period ? 2 : 0), complement.EnumerateObject().Count());
        Assert.Equal(_course.ToString("D"), complement.GetProperty("curso").GetString());
        if (price) { Assert.Equal("49700", complement.GetProperty("precoAnterior").GetString()); Assert.Equal("39700", complement.GetProperty("precoNovo").GetString()); }
        if (period) { Assert.Equal("12m", complement.GetProperty("vigenciaAnterior").GetString()); Assert.Equal(newPeriod, complement.GetProperty("vigenciaNova").GetString()); }
        Assert.False(act.TryGetProperty("motivo", out _)); Assert.False(act.TryGetProperty("name", out _)); Assert.False(act.TryGetProperty("priceCents", out _));
    }

    private async Task AssertUnchangedAsync(CatalogCourseApiFactory factory)
    {
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var stored = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal(49700, stored.PriceCents); Assert.Equal(2, stored.OfferRevision); Assert.Equal("published", stored.Status);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Empty(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    private async Task<CatalogOfferDetail> SeedAsync(CatalogCourseApiFactory factory, string status = "published")
    {
        await using var scope = Scope(factory);
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(_tenant, _course, rich: true, level: "beginner")), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); var course = await db.CatalogCourseViews.SingleAsync(Cancellation);
        var offer = course.CreateOffer(new("Option", 49700, AccessPeriod.Create("months", 12)), DateTimeOffset.UtcNow);
        if (status != "draft") course.PublishOffer(offer.OfferId, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Cancellation);
        if (status == "unpublished")
            await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {offer.OfferId}", Cancellation);
        await db.Entry(offer).ReloadAsync(Cancellation);
        return CatalogOfferDetail.FromCatalogOffer(offer);
    }

    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant); return scope;
    }

    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, Guid offerId, object body, string? key,
        Guid? tenant = null, string permission = "oferta.editar")
    {
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", _actor.ToString()), new Claim("tenantId", (tenant ?? _tenant).ToString()), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/internal/v1/catalog/offers/{offerId:D}") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        if (key is not null) request.Headers.Add("Idempotency-Key", key); request.Headers.Add("traceparent", Trace);
        return client.SendAsync(request, Cancellation);
    }

    private async Task<CatalogCourseApiFactory> BrokerFactoryAsync()
    {
        // The hosted publisher drains every tenant, so give broker scenarios an isolated database.
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgreSql.GetConnectionString());
        await using (var connection = new NpgsqlConnection(connectionString.ConnectionString))
        {
            await connection.OpenAsync(Cancellation);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE change_{_tenant:N}";
            await command.ExecuteNonQueryAsync(Cancellation);
        }
        connectionString.Database = $"change_{_tenant:N}";
        await using (var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseNpgsql(connectionString.ConnectionString).Options, new TenantContext()))
            await db.Database.MigrateAsync(Cancellation);
        var factory = new CatalogCourseApiFactory(fixture) { DatabaseConnectionString = connectionString.ConnectionString };
        Action<IServiceCollection> configure = services => services.Configure<OutboxOptions>(options => options.PollingIntervalSeconds = 1);
        factory.CustomizeServices = services =>
        {
            configure!(services);
            services.Configure<RabbitMqOptions>(options =>
            {
                options.Exchange = $"commerce-{_tenant:D}"; options.AuditExchange = $"audit-{_tenant:D}";
                options.HeartbeatQueue = $"heartbeat-{_tenant:D}"; options.CatalogCourseQueue = $"courses-{_tenant:D}";
                options.OfferRetentionQueue = $"retention-{_tenant:D}";
            });
        };
        return factory;
    }

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(50, timeout.Token);
    }

    private static async Task<BasicGetResult> GetAsync(IChannel channel, string queue)
    {
        BasicGetResult? result = null;
        await WaitAsync(async () => (result = await channel.BasicGetAsync(queue, true, Cancellation)) is not null);
        return result!;
    }
}
