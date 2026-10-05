using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class OrderCreationTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private OrderFixture Fixture() { var tenant = Guid.CreateVersion7(); return new(hosts.Showcase(tenant), tenant); }
    [Fact(DisplayName = nameof(CreatesSnapshotAndTransactionalContractFact))]
    public async Task CreatesSnapshotAndTransactionalContractFact()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var response = await f.CreateAsync(ids[1], "new");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var body = await OrderFixture.BodyAsync(response);
        OrderHttpContract.AssertValid(body, "StudentOrder");
        Assert.Equal(49700, body.GetProperty("priceCents").GetInt32()); Assert.Equal(12, body.GetProperty("accessPeriod").GetProperty("months").GetInt32());
        Assert.Equal("awaiting-payment", body.GetProperty("status").GetString()); Assert.Equal("000001", body.GetProperty("number").GetString());
        Assert.Equal($"/internal/v1/orders/{body.GetProperty("orderId").GetGuid()}", response.Headers.Location!.ToString());
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Single(await db.Orders.ToListAsync(OrderFixture.Cancellation)); Assert.Single(await db.OrderReceipts.ToListAsync(OrderFixture.Cancellation));
        var fact = Assert.Single(await db.OutboxMessages.Where(x => x.RoutingKey == "vendas.pedido-criado.v1").ToListAsync(OrderFixture.Cancellation));
        CommerceMessages.AssertSends(fact.RoutingKey, fact.Payload);
    }
    [Fact(DisplayName = nameof(ChangingOfferDoesNotChangeFrozenOrder))]
    public async Task ChangingOfferDoesNotChangeFrozenOrder()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var response = await f.CreateAsync(ids[1], "snapshot"); var created = await OrderFixture.BodyAsync(response);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var course = await db.CatalogCourseViews.Include(x => x.Offers).SingleAsync(x => x.CourseId == ids[0], OrderFixture.Cancellation);
        course.UpdateOffer(ids[1], new("Changed", 59700, AccessPeriod.Create("months", 6)), DateTimeOffset.UtcNow); await db.SaveChangesAsync(OrderFixture.Cancellation);
        using var client = f.Client(); using var read = await client.GetAsync($"/internal/v1/orders/{created.GetProperty("orderId").GetGuid()}", OrderFixture.Cancellation);
        var frozen = await OrderFixture.BodyAsync(read); AssertFrozen(created, frozen);
    }
    [Fact(DisplayName = nameof(UnpublishingAfterCreationDoesNotChangeOrder))]
    public async Task UnpublishingAfterCreationDoesNotChangeOrder()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var response = await f.CreateAsync(ids[1], "frozen"); var body = await OrderFixture.BodyAsync(response);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {ids[1]}", OrderFixture.Cancellation);
        using var client = f.Client(); using var read = await client.GetAsync($"/internal/v1/orders/{body.GetProperty("orderId").GetGuid()}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode); AssertFrozen(body, await OrderFixture.BodyAsync(read));
    }
    [Fact(DisplayName = nameof(UnpublishedBeforeCreationReturns404AndNoOrder))]
    public async Task UnpublishedBeforeCreationReturns404AndNoOrder()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {ids[1]}", OrderFixture.Cancellation);
        using var response = await f.CreateAsync(ids[1], "unpublished"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("OFFER_NOT_AVAILABLE", (await OrderFixture.BodyAsync(response)).GetProperty("code").GetString());
        Assert.Empty(await db.Orders.ToListAsync(OrderFixture.Cancellation)); Assert.Empty(await db.OrderReceipts.ToListAsync(OrderFixture.Cancellation));
    }
    [Fact(DisplayName = nameof(ConcurrentCreationsReturnOnePendingOrderAndOneFact))]
    public async Task ConcurrentCreationsReturnOnePendingOrderAndOneFact()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => f.CreateAsync(ids[1], $"concurrent-{i}")));
        var orderIds = new List<Guid>();
        foreach (var response in responses) { using (response) { Assert.Contains((int)response.StatusCode, new[] { 200, 201 }); orderIds.Add((await OrderFixture.BodyAsync(response)).GetProperty("orderId").GetGuid()); } }
        Assert.Single(orderIds.Distinct()); await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Single(await db.Orders.ToListAsync(OrderFixture.Cancellation)); Assert.Single(await db.OutboxMessages.Where(x => x.RoutingKey == "vendas.pedido-criado.v1").ToListAsync(OrderFixture.Cancellation));
    }
    [Fact(DisplayName = nameof(AnotherOfferOfSameCourseCreatesSecondOrder))]
    public async Task AnotherOfferOfSameCourseCreatesSecondOrder()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var first = await f.CreateAsync(ids[1], "first"); using var second = await f.CreateAsync(ids[2], "second");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode); Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual((await OrderFixture.BodyAsync(first)).GetProperty("orderId").GetGuid(), (await OrderFixture.BodyAsync(second)).GetProperty("orderId").GetGuid());
    }
    [Fact(DisplayName = nameof(ReplayKeepsBodyAndOriginalCreatedStatusEvenAfterUnpublication))]
    public async Task ReplayKeepsBodyAndOriginalCreatedStatusEvenAfterUnpublication()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var first = await f.CreateAsync(ids[1], "replay"); var body = await first.Content.ReadAsStringAsync(OrderFixture.Cancellation);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE catalog.offers SET status = 'unpublished' WHERE offer_id = {ids[1]}", OrderFixture.Cancellation);
        using var retry = await f.CreateAsync(ids[1], "replay"); Assert.Equal(first.StatusCode, retry.StatusCode); Assert.Equal(body, await retry.Content.ReadAsStringAsync(OrderFixture.Cancellation));
        Assert.Single(await db.OutboxMessages.Where(x => x.RoutingKey == "vendas.pedido-criado.v1").ToListAsync(OrderFixture.Cancellation));
    }
    [Fact(DisplayName = nameof(ReusingKeyWithAnotherBodyReturns422))]
    public async Task ReusingKeyWithAnotherBodyReturns422()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var first = await f.CreateAsync(ids[1], "same"); using var changed = await f.CreateAsync(ids[2], "same");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, changed.StatusCode); Assert.Equal("IDEMPOTENCY_KEY_REUSED", (await OrderFixture.BodyAsync(changed)).GetProperty("code").GetString());
    }
    [Fact(DisplayName = nameof(ActorWithStudentScopeIsDeniedWithoutOrder))]
    public async Task ActorWithStudentScopeIsDeniedWithoutOrder()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var client = f.Client(actor: true); using var response = await OrderFixture.CreateAsync(client, ids[1], "actor");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal("SCOPE_DENIED", (await OrderFixture.BodyAsync(response)).GetProperty("code").GetString());
        await using var scope = f.Scope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Orders.ToListAsync(OrderFixture.Cancellation));
    }
    [Fact(DisplayName = nameof(OtherStudentsOrderIsIndistinguishableFromMissing))]
    public async Task OtherStudentsOrderIsIndistinguishableFromMissing()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var created = await f.CreateAsync(ids[1], "owner"); var id = (await OrderFixture.BodyAsync(created)).GetProperty("orderId").GetGuid();
        using var other = f.Client(Guid.CreateVersion7()); using var response = await other.GetAsync($"/internal/v1/orders/{id}", OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal("ORDER_NOT_FOUND", (await OrderFixture.BodyAsync(response)).GetProperty("code").GetString());
    }
    [Fact(DisplayName = nameof(OtherSchoolsOfferCannotBePurchased))]
    public async Task OtherSchoolsOfferCannotBePurchased()
    {
        var first = Fixture(); var ids = await first.SeedAsync(); var other = Fixture(); using var response = await other.CreateAsync(ids[1], "foreign"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact(DisplayName = nameof(SequentialNumbersAreIndependentPerSchoolAndPendingReplayStays200))]
    public async Task SequentialNumbersAreIndependentPerSchoolAndPendingReplayStays200()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var one = await f.CreateAsync(ids[1], "one"); using var pending = await f.CreateAsync(ids[1], "pending"); using var retry = await f.CreateAsync(ids[1], "pending"); using var two = await f.CreateAsync(ids[2], "two");
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode); Assert.Equal(pending.StatusCode, retry.StatusCode); Assert.Equal("000002", (await OrderFixture.BodyAsync(two)).GetProperty("number").GetString());
        var other = Fixture(); var otherIds = await other.SeedAsync(); using var foreign = await other.CreateAsync(otherIds[1], "one"); Assert.Equal("000001", (await OrderFixture.BodyAsync(foreign)).GetProperty("number").GetString());
    }
    [Fact(DisplayName = nameof(MissingKeyOrClientPriceIsRejectedWithoutPersisting))]
    public async Task MissingKeyOrClientPriceIsRejectedWithoutPersisting()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var client = f.Client(); using var noKey = await client.PostAsJsonAsync("/internal/v1/orders", new { offerId = ids[1] }, OrderFixture.Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/orders") { Content = JsonContent.Create(new { offerId = ids[1], priceCents = 1 }) }; request.Headers.Add("Idempotency-Key", "forged");
        using var forged = await client.SendAsync(request, OrderFixture.Cancellation); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        await using var scope = f.Scope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<CommerceDbContext>().Orders.ToListAsync(OrderFixture.Cancellation));
    }

    [Fact(DisplayName = nameof(ReceiptStoresHashesAndExpiresAfter24Hours))]
    public async Task ReceiptStoresHashesAndExpiresAfter24Hours()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); var before = DateTimeOffset.UtcNow;
        using var response = await f.CreateAsync(ids[1], "opaque-key"); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var receipt = await db.OrderReceipts.SingleAsync(OrderFixture.Cancellation);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("opaque-key"))), receipt.KeyHash);
        Assert.InRange(receipt.ExpiresAt, before.AddHours(24), DateTimeOffset.UtcNow.AddHours(24)); Assert.Equal(201, receipt.StatusCode);
        var expired = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.Database.ExecuteSqlAsync($"UPDATE sales.order_receipts SET expires_at = {expired} WHERE tenant_id = {f.Tenant}", OrderFixture.Cancellation);
        using var next = await f.CreateAsync(ids[2], "opaque-key"); Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }
    [Fact(DisplayName = nameof(SameIdempotencyKeyIsScopedToTheStudent))]
    public async Task SameIdempotencyKeyIsScopedToTheStudent()
    {
        var f = Fixture(); var ids = await f.SeedAsync(); using var one = await f.CreateAsync(ids[1], "shared"); using var two = await f.CreateAsync(ids[2], "shared", Guid.CreateVersion7());
        Assert.Equal(HttpStatusCode.Created, one.StatusCode); Assert.Equal(HttpStatusCode.Created, two.StatusCode);
    }

    [Fact(DisplayName = nameof(FailureAfterSaveRollsBackOrderReceiptCounterAndFact))]
    public async Task FailureAfterSaveRollsBackOrderReceiptCounterAndFact()
    {
        var f = Fixture(); var ids = await f.SeedAsync();
        using var failing = f.Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Scoped<CodeForCoders.Commerce.Application.Interfaces.IUnitOfWork, CourtesyFailingCommit>())));
        using var original = f.Client(); using var client = failing.CreateClient(); client.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        using var response = await OrderFixture.CreateAsync(client, ids[1], "rollback"); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var scope = f.Scope(); var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        Assert.Empty(await db.Orders.ToListAsync(OrderFixture.Cancellation)); Assert.Empty(await db.OrderReceipts.ToListAsync(OrderFixture.Cancellation));
        Assert.Empty(await db.OrderSequences.ToListAsync(OrderFixture.Cancellation)); Assert.Empty(await db.OutboxMessages.ToListAsync(OrderFixture.Cancellation));
    }
    private static void AssertFrozen(System.Text.Json.JsonElement expected, System.Text.Json.JsonElement actual)
    {
        var before = System.Text.Json.Nodes.JsonNode.Parse(expected.GetRawText())!.AsObject();
        var after = System.Text.Json.Nodes.JsonNode.Parse(actual.GetRawText())!.AsObject();
        Assert.InRange((expected.GetProperty("createdAt").GetDateTimeOffset() - actual.GetProperty("createdAt").GetDateTimeOffset()).Duration(), TimeSpan.Zero, TimeSpan.FromMicroseconds(1));
        before.Remove("createdAt"); after.Remove("createdAt");
        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

}
