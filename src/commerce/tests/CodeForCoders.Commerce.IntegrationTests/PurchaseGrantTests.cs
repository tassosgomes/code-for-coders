using System.Text.Json;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using CodeForCoders.Commerce.Domain.ValueObjects;
using CodeForCoders.Commerce.Infra.Data;
using CodeForCoders.Commerce.Infra.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeForCoders.Commerce.IntegrationTests;

[Collection(CommerceIntegrationCollection.Name)]
public sealed class PurchaseGrantTests(CommerceHosts hosts) : IClassFixture<CommerceHosts>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private async Task<(ShowcaseApiFactory factory, Guid tenant, Guid courseId, Guid studentId)> SetupTestAsync()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        var courseId = Guid.CreateVersion7();
        var studentId = Guid.CreateVersion7();

        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenant);
        var publishedFact = PublishedCourseFact.Parse(CatalogCourseFactFixture.Create(tenant, courseId, title: "Curso .NET Pro"));
        await scope.ServiceProvider.GetRequiredService<IEntitlementCourseProjectionStore>().ApplyAsync(publishedFact, Cancellation);

        return (factory, tenant, courseId, studentId);
    }

    [Fact(DisplayName = nameof(PurchaseGrant_CreatesAccessGrantWithPurchaseOriginAndRef))]
    public async Task PurchaseGrant_CreatesAccessGrantWithPurchaseOriginAndRef()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId,
            "000001",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var grant = await db.AccessGrants.SingleAsync(g => g.OriginRef == orderId, Cancellation);
            Assert.Equal("purchase", grant.Origin);
            Assert.Equal(orderId, grant.OriginRef);
            Assert.Equal(studentId, grant.StudentId);
            Assert.Equal(courseId, grant.CourseId);
            Assert.Equal("active", grant.Status);
            Assert.Equal("months", grant.PeriodType);
            Assert.Equal(12, grant.PeriodMonths);
            Assert.NotNull(grant.EndsOn);
            Assert.NotNull(grant.ExpiresAt);

            var enrollment = await db.Enrollments.SingleAsync(e => e.Id == grant.EnrollmentId, Cancellation);
            Assert.Equal(studentId, enrollment.StudentId);
            Assert.Equal(courseId, enrollment.CourseId);
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_PublishesAcessoConcedidoFactInOutbox))]
    public async Task PurchaseGrant_PublishesAcessoConcedidoFactInOutbox()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId,
            "000002",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var messages = await db.EntitlementOutboxMessages
                .Where(m => m.RoutingKey == "matricula.acesso-concedido.v1")
                .ToListAsync(Cancellation);
            var message = Assert.Single(messages);

            CommerceMessages.AssertSends(message.RoutingKey, message.Payload);

            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;
            Assert.Equal(tenant, root.GetProperty("tenantId").GetGuid());
            Assert.Equal(studentId, root.GetProperty("studentId").GetGuid());
            Assert.Equal(courseId, root.GetProperty("courseId").GetGuid());
            Assert.Equal("purchase", root.GetProperty("origin").GetString());
            Assert.Equal(orderId, root.GetProperty("originRef").GetGuid());
            Assert.Equal("months", root.GetProperty("accessPeriod").GetProperty("type").GetString());
            Assert.Equal(12, root.GetProperty("accessPeriod").GetProperty("months").GetInt32());
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_RedeliveryIsIdempotent))]
    public async Task PurchaseGrant_RedeliveryIsIdempotent()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId,
            "000003",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact, Cancellation);
            // Redelivery of the exact same fact
            await sink.ApplyAsync(fact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var grants = await db.AccessGrants.Where(g => g.OriginRef == orderId).ToListAsync(Cancellation);
            Assert.Single(grants);

            var messages = await db.EntitlementOutboxMessages
                .Where(m => m.RoutingKey == "matricula.acesso-concedido.v1")
                .ToListAsync(Cancellation);
            Assert.Single(messages);
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_Calculates12MonthsTermCorrectly))]
    public async Task PurchaseGrant_Calculates12MonthsTermCorrectly()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId,
            "000004",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var grant = await db.AccessGrants.SingleAsync(g => g.OriginRef == orderId, Cancellation);
            Assert.NotNull(grant.EndsOn);
            // In Brasilia timezone, 12 months after now
            var expectedYear = now.Year + 1;
            Assert.Equal(expectedYear, grant.EndsOn.Value.Year);
            Assert.NotNull(grant.ExpiresAt);
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_LifetimeGrant_HasNoExpiration))]
    public async Task PurchaseGrant_LifetimeGrant_HasNoExpiration()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId,
            "000005",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            89700,
            89700,
            "BRL",
            AccessPeriod.Create("lifetime", null),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var grant = await db.AccessGrants.SingleAsync(g => g.OriginRef == orderId, Cancellation);
            Assert.Equal("lifetime", grant.PeriodType);
            Assert.Null(grant.PeriodMonths);
            Assert.Null(grant.EndsOn);
            Assert.Null(grant.ExpiresAt);
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_CoexistsWithExistingEnrollment))]
    public async Task PurchaseGrant_CoexistsWithExistingEnrollment()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();
        var orderId1 = Guid.CreateVersion7();
        var orderId2 = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;

        var fact1 = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId1,
            "000006",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            now,
            now);

        var fact2 = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            orderId2,
            "000007",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            89700,
            89700,
            "BRL",
            AccessPeriod.Create("lifetime", null),
            "card",
            now,
            now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();
            await sink.ApplyAsync(fact1, Cancellation);
            await sink.ApplyAsync(fact2, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            // Exactly 1 Enrollment shared by both grants
            var enrollment = await db.Enrollments.SingleAsync(e => e.StudentId == studentId && e.CourseId == courseId, Cancellation);
            var grants = await db.AccessGrants.Where(g => g.EnrollmentId == enrollment.Id).ToListAsync(Cancellation);
            Assert.Equal(2, grants.Count);
        }
    }

    [Fact(DisplayName = nameof(SalesAccessSink_RecordsGrantOnOrder))]
    public async Task SalesAccessSink_RecordsGrantOnOrder()
    {
        var tenant = Guid.CreateVersion7();
        var factory = hosts.Showcase(tenant);
        var orderFixture = new OrderFixture(factory, tenant);
        var ids = await orderFixture.SeedAsync();
        using var createResp = await orderFixture.CreateAsync(ids[1], "key-access-sink");
        var created = await OrderFixture.BodyAsync(createResp);
        var orderId = created.GetProperty("orderId").GetGuid();
        var grantId = Guid.CreateVersion7();
        var grantedAt = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var paymentSink = scope.ServiceProvider.GetRequiredService<ISalesPaymentSink>();
            await paymentSink.ApplyAsync(new PaymentConfirmedFact(
                Guid.CreateVersion7(),
                tenant,
                Guid.CreateVersion7(),
                orderId,
                "card",
                49700,
                "BRL",
                "pi_access_sink",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow), Cancellation);

            var accessSink = scope.ServiceProvider.GetRequiredService<ISalesAccessSink>();
            var accessFact = new PurchaseAccessFact(
                Guid.CreateVersion7(),
                tenant,
                grantId,
                orderFixture.Student,
                ids[0],
                "purchase",
                orderId,
                grantedAt);
            await accessSink.ApplyAsync(accessFact, Cancellation);

            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId, Cancellation);
            Assert.Equal(grantId, order.GrantId);
            Assert.NotNull(order.AccessGrantedAt);
        }
    }

    [Fact(DisplayName = nameof(PurchaseGrant_RejectsInvalidFactWithException))]
    public async Task PurchaseGrant_RejectsInvalidFactWithException()
    {
        var (factory, tenant, courseId, studentId) = await SetupTestAsync();

        var invalidFact = new PurchaseCompletedFact(
            Guid.Empty,
            tenant,
            Guid.CreateVersion7(),
            "000008",
            studentId,
            courseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using var scope = factory.Services.CreateAsyncScope();
        var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();

        var ex = await Assert.ThrowsAsync<EntitlementRuleException>(() =>
            sink.ApplyAsync(invalidFact, Cancellation));
        Assert.Equal("PURCHASE_INVALID", ex.Code);
    }

    [Fact(DisplayName = nameof(PurchaseGrant_UnknownCourse_ThrowsException))]
    public async Task PurchaseGrant_UnknownCourse_ThrowsException()
    {
        var (factory, tenant, _, studentId) = await SetupTestAsync();
        var unknownCourseId = Guid.CreateVersion7();

        var fact = new PurchaseCompletedFact(
            Guid.CreateVersion7(),
            tenant,
            Guid.CreateVersion7(),
            "000009",
            studentId,
            unknownCourseId,
            Guid.CreateVersion7(),
            49700,
            49700,
            "BRL",
            AccessPeriod.Create("months", 12),
            "card",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await using var scope = factory.Services.CreateAsyncScope();
        var sink = scope.ServiceProvider.GetRequiredService<IPurchaseGrantSink>();

        var ex = await Assert.ThrowsAsync<EntitlementRuleException>(() =>
            sink.ApplyAsync(fact, Cancellation));
        Assert.Equal("COURSE_UNKNOWN", ex.Code);
    }
}
