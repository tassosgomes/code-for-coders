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
using CodeForCoders.Commerce.Infra.Data.Health;
using CodeForCoders.Commerce.Infra.Data.Outbox;
using CodeForCoders.Commerce.Infra.Messaging;
using CodeForCoders.Commerce.Infra.Messaging.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using RabbitMQ.Client;
using Npgsql;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OfferPublicationTests(CommerceIntegrationFixture fixture)
{
    private readonly Guid _tenant = Guid.CreateVersion7();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _course = Guid.CreateVersion7();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private const string Trace = "00-708192a3b4c5d6e7f8091a2b3c4d5e6f-708192a3b4c5d6e7-01";
    private static string Path(Guid id) => $"/internal/v1/catalog/offers/{id:D}/publish";

    private CatalogCourseApiFactory Factory(bool worker = false) => new(fixture)
    {
        CustomizeServices = services => services.Configure<OutboxOptions>(options => options.PollingIntervalSeconds = worker ? 1 : 60)
    };

    [Fact(DisplayName = nameof(PublicationAndOutboxMatchApprovedContractsAndAuthor))]
    public async Task PublicationAndOutboxMatchApprovedContractsAndAuthor()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId, "publish");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CatalogOfferDetail>(Cancellation))!;
        Assert.Equal("published", result.Status); Assert.NotNull(result.PublishedAt);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Equal(2, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
        Assert.True((await db.CatalogCourseViews.SingleAsync(Cancellation)).InShowcase);
        var rows = await db.CatalogOutboxMessages.ToListAsync(Cancellation);
        AssertPair(rows);
        Assert.Empty(await db.OutboxMessages.ToListAsync(Cancellation));
        Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(CourseWithoutLevelRefusesWithoutMessagesOrReceipt))]
    public async Task CourseWithoutLevelRefusesWithoutMessagesOrReceipt()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory, null);
        using var response = await SendAsync(factory, client, offer.OfferId, "no-level");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("COURSE_LEVEL_REQUIRED", await response.Content.ReadAsStringAsync(Cancellation));
        await AssertDraftAsync(factory);
    }

    [Fact(DisplayName = nameof(ConcurrentSameKeyReturnsOneResponseAndOnePair))]
    public async Task ConcurrentSameKeyReturnsOneResponseAndOnePair()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(factory, client, offer.OfferId, "same")));
        var json = await responses[0].Content.ReadAsStringAsync(Cancellation);
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(json, await response.Content.ReadAsStringAsync(Cancellation)); response.Dispose();
        }
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        AssertPair(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Single(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(NewIntentOnPublishedOfferConflictsAndKeyCannotTargetAnotherOffer))]
    public async Task NewIntentOnPublishedOfferConflictsAndKeyCannotTargetAnotherOffer()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var published = await SendAsync(factory, client, offer.OfferId, "first"); Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using var conflict = await SendAsync(factory, client, offer.OfferId, "second");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, conflict.StatusCode); Assert.Contains("OFFER_STATE_CONFLICT", await conflict.Content.ReadAsStringAsync(Cancellation));
        using var reused = await SendAsync(factory, client, Guid.CreateVersion7(), "first");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode); Assert.Contains("IDEMPOTENCY_KEY_REUSED", await reused.Content.ReadAsStringAsync(Cancellation));
        await using var scope = Scope(factory); Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOutboxMessages.CountAsync(Cancellation));
    }

    [Fact(DisplayName = nameof(RepublicationCreatesNewRevisionFactAndAct))]
    public async Task RepublicationCreatesNewRevisionFactAndAct()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var first = await SendAsync(factory, client, offer.OfferId, "first"); Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using (var scope = Scope(factory))
            await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Database.ExecuteSqlAsync(
                $"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {offer.OfferId}", Cancellation);
        using var second = await SendAsync(factory, client, offer.OfferId, "republish"); Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await using var check = Scope(factory); var db = check.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var facts = await db.CatalogOutboxMessages.Where(row => row.RoutingKey == "catalogo.oferta-publicada.v1").OrderBy(row => row.Id).ToListAsync(Cancellation);
        Assert.Equal(2, facts.Count); Assert.NotEqual(facts[0].Id, facts[1].Id);
        Assert.Equal(3, (await db.CatalogOffers.SingleAsync(Cancellation)).OfferRevision);
        Assert.Equal(2, await db.CatalogOutboxMessages.CountAsync(row => row.RoutingKey == "auditoria.ato-praticado.v1", Cancellation));
        foreach (var fact in facts) OfferPublicationContract.AssertValid(JsonDocument.Parse(fact.Payload).RootElement);
    }

    [Fact(DisplayName = nameof(FailureAfterSaveBeforeTransactionCommitRollsBackEverything))]
    public async Task FailureAfterSaveBeforeTransactionCommitRollsBackEverything()
    {
        await using var factory = Factory(); var configure = factory.CustomizeServices;
        factory.CustomizeServices = services =>
        {
            configure!(services); services.RemoveAll<IUnitOfWork>(); services.AddScoped<IUnitOfWork, FailAfterSaveUnitOfWork>();
        };
        using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var response = await SendAsync(factory, client, offer.OfferId, "rollback");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await AssertDraftAsync(factory);
    }

    [Fact(DisplayName = nameof(PublicationReadsTheCurrentLevelUnderCourseLock))]
    public async Task PublicationReadsTheCurrentLevelUnderCourseLock()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(Cancellation);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM catalog.course_views WHERE course_id = {_course} FOR UPDATE", Cancellation);
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.course_views SET level = NULL WHERE course_id = {_course}", Cancellation);
        var request = SendAsync(factory, client, offer.OfferId, "locked");
        await Task.Delay(150, Cancellation); Assert.False(request.IsCompleted);
        await transaction.CommitAsync(Cancellation);
        using var response = await request; Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("COURSE_LEVEL_REQUIRED", await response.Content.ReadAsStringAsync(Cancellation));
        await AssertDraftAsync(factory);
    }

    [Fact(DisplayName = nameof(MissingKeyForeignTenantAndPermissionCannotPublish))]
    public async Task MissingKeyForeignTenantAndPermissionCannotPublish()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var offer = await SeedAsync(factory);
        using var absent = await SendAsync(factory, client, offer.OfferId, null); Assert.Equal(HttpStatusCode.BadRequest, absent.StatusCode);
        using var foreign = await SendAsync(factory, client, offer.OfferId, "foreign", Guid.CreateVersion7()); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var missing = await SendAsync(factory, client, Guid.CreateVersion7(), "missing"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var denied = await SendAsync(factory, client, offer.OfferId, "denied", permission: "autoria.editar"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await AssertDraftAsync(factory);
    }

    [Fact(DisplayName = nameof(RealHostPublishesBothOutboxesToCorrectExchangesWithCorrelation))]
    public async Task RealHostPublishesBothOutboxesToCorrectExchangesWithCorrelation()
    {
        await using var factory = await BrokerFactoryAsync(); using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        var auditQueue = $"offer-audit-{_tenant:D}";
        await channel.QueueDeclareAsync(auditQueue, false, true, true, cancellationToken: Cancellation);
        await channel.QueueBindAsync(auditQueue, $"audit-{_tenant:D}", "auditoria.ato-praticado.v1", cancellationToken: Cancellation);
        var offer = await SeedAsync(factory); using var response = await SendAsync(factory, client, offer.OfferId, "broker");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Sales heartbeat still shares the real hosted publisher.
        await using (var scope = Scope(factory))
        {
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            db.OutboxMessages.Add(OutboxMessage.Create(new(Guid.CreateVersion7(), _tenant, "Heartbeat", "commerce.platform.heartbeat.v1",
                new { }, DateTimeOffset.UtcNow, Trace), "{}"));
            await db.SaveChangesAsync(Cancellation);
        }
        var fact = await GetAsync(channel, $"retention-{_tenant:D}");
        var act = await GetAsync(channel, auditQueue);
        Assert.Equal($"commerce-{_tenant:D}", fact.Exchange); Assert.Equal($"audit-{_tenant:D}", act.Exchange);
        OfferPublicationContract.AssertValid(JsonDocument.Parse(fact.Body).RootElement);
        OfferPublicationContract.AssertValid(JsonDocument.Parse(act.Body).RootElement, true);
        Assert.Equal(JsonDocument.Parse(fact.Body).RootElement.GetProperty("eventId").GetGuid(),
            JsonDocument.Parse(act.Body).RootElement.GetProperty("fatoId").GetGuid());
        var headers = act.BasicProperties.Headers!;
        var correlation = Encoding.UTF8.GetString((byte[])headers["correlationId"]!);
        Assert.Equal(correlation, Encoding.UTF8.GetString((byte[])headers["traceparent"]!));
        Assert.StartsWith(Trace[..35], correlation);
        await WaitAsync(async () =>
        {
            await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            return await db.CatalogOutboxMessages.AllAsync(row => row.ProcessedOn != null, Cancellation)
                && await db.OutboxMessages.AllAsync(row => row.ProcessedOn != null, Cancellation);
        });
    }

    [Fact(DisplayName = nameof(MissingRetentionQueueExhaustsFactAttemptsAndDegradesCatalogHealth))]
    public async Task MissingRetentionQueueExhaustsFactAttemptsAndDegradesCatalogHealth()
    {
        await using var factory = await BrokerFactoryAsync(); using var client = factory.CreateClient();
        await using var channel = await factory.Services.GetRequiredService<RabbitMqConnectionProvider>().CreateChannelAsync(Cancellation);
        await channel.QueueDeleteAsync($"retention-{_tenant:D}", false, false, Cancellation);
        var offer = await SeedAsync(factory); using var response = await SendAsync(factory, client, offer.OfferId, "unroutable");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitAsync(async () =>
        {
            await using var scope = Scope(factory);
            return await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().CatalogOutboxMessages
                .AnyAsync(row => row.RoutingKey == "catalogo.oferta-publicada.v1" && row.Attempts == 10, Cancellation);
        });
        await using var check = Scope(factory); var db = check.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var fact = await db.CatalogOutboxMessages.SingleAsync(row => row.RoutingKey == "catalogo.oferta-publicada.v1", Cancellation);
        Assert.Null(fact.ProcessedOn);
        Assert.Equal(HealthStatus.Degraded, (await new OutboxHealthCheck(db).CheckHealthAsync(new(), Cancellation)).Status);
        await Task.Delay(1100, Cancellation); await db.Entry(fact).ReloadAsync(Cancellation); Assert.Equal(10, fact.Attempts);
    }

    private async Task<CatalogCourseApiFactory> BrokerFactoryAsync()
    {
        // The hosted publisher drains every tenant, so give broker scenarios an isolated database.
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.PostgreSql.GetConnectionString());
        await using (var connection = new NpgsqlConnection(connectionString.ConnectionString))
        {
            await connection.OpenAsync(Cancellation);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE publication_{_tenant:N}";
            await command.ExecuteNonQueryAsync(Cancellation);
        }
        connectionString.Database = $"publication_{_tenant:N}";
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

    private void AssertPair(List<CatalogOutboxMessage> rows)
    {
        Assert.Equal(2, rows.Count);
        var fact = rows.Single(row => row.RoutingKey == "catalogo.oferta-publicada.v1");
        var act = rows.Single(row => row.RoutingKey == "auditoria.ato-praticado.v1");
        using var factJson = JsonDocument.Parse(fact.Payload); using var actJson = JsonDocument.Parse(act.Payload);
        OfferPublicationContract.AssertValid(factJson.RootElement); OfferPublicationContract.AssertValid(actJson.RootElement, true);
        Assert.Equal(fact.Id, actJson.RootElement.GetProperty("fatoId").GetGuid());
        Assert.Equal(_actor, actJson.RootElement.GetProperty("autor").GetProperty("id").GetGuid());
        Assert.Equal(_tenant, actJson.RootElement.GetProperty("tenantId").GetGuid());
        Assert.Equal(_course.ToString("D"), actJson.RootElement.GetProperty("complemento").GetProperty("curso").GetString());
        Assert.Equal(factJson.RootElement.GetProperty("occurredAt").GetDateTimeOffset(), actJson.RootElement.GetProperty("praticadoEm").GetDateTimeOffset());
        Assert.False(actJson.RootElement.TryGetProperty("motivo", out _)); Assert.False(actJson.RootElement.TryGetProperty("name", out _));
        Assert.False(actJson.RootElement.TryGetProperty("priceCents", out _));
    }

    private async Task AssertDraftAsync(CatalogCourseApiFactory factory)
    {
        await using var scope = Scope(factory); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var offer = await db.CatalogOffers.SingleAsync(Cancellation);
        Assert.Equal("draft", offer.Status); Assert.Equal(1, offer.OfferRevision); Assert.Null(offer.PublishedAt);
        Assert.False((await db.CatalogCourseViews.SingleAsync(Cancellation)).InShowcase);
        Assert.Empty(await db.CatalogOutboxMessages.ToListAsync(Cancellation)); Assert.Empty(await db.CatalogEditReceipts.ToListAsync(Cancellation));
    }

    private async Task<CatalogOfferDetail> SeedAsync(CatalogCourseApiFactory factory, string? level = "beginner")
    {
        await using var scope = Scope(factory);
        await scope.ServiceProvider.GetRequiredService<ICatalogCourseProjectionStore>().ApplyAsync(
            PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(_tenant, _course, rich: true, level: level)), Cancellation);
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>(); var course = await db.CatalogCourseViews.SingleAsync(Cancellation);
        var offer = course.CreateOffer(new("Acesso por 12 meses", 49700, AccessPeriod.Create("months", 12)), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Cancellation); return CatalogOfferDetail.FromCatalogOffer(offer);
    }

    private AsyncServiceScope Scope(CatalogCourseApiFactory factory)
    {
        var scope = factory.Services.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(_tenant); return scope;
    }

    private Task<HttpResponseMessage> SendAsync(CatalogCourseApiFactory factory, HttpClient client, Guid offerId, string? key,
        Guid? tenant = null, string permission = "oferta.editar")
    {
        var token = new JwtSecurityToken("identity", "commerce",
            [new Claim("sub", _actor.ToString()), new Claim("tenantId", (tenant ?? _tenant).ToString()), new Claim("permissions", permission)],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(factory.JwksHandler.SigningKey, SecurityAlgorithms.RsaSha256));
        var request = new HttpRequestMessage(HttpMethod.Post, Path(offerId)); request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        if (key is not null) request.Headers.Add("Idempotency-Key", key); request.Headers.Add("traceparent", Trace);
        return client.SendAsync(request, Cancellation);
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

public sealed class FailAfterSaveUnitOfWork(CommerceDbContext dbContext) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("Forced failure before catalog transaction commit.");
    }
}
