using System.Diagnostics;
using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class PurchaseGrantSink(CommerceDbContext db, ITenantContext tenant, ICourtesyGrantStore enrollmentStore,
 IEntitlementOutboxMessageWriter outbox, IUnitOfWork unitOfWork, TimeProvider clock, SchoolTimeZone zone) : IPurchaseGrantSink
{
    public async Task ApplyAsync(PurchaseCompletedFact fact, CancellationToken cancellationToken)
    {
        if (fact.EventId == Guid.Empty || fact.TenantId == Guid.Empty || fact.OrderId == Guid.Empty
         || fact.StudentId == Guid.Empty || fact.CourseId == Guid.Empty || fact.AccessPeriod is null)
            throw new EntitlementRuleException("PURCHASE_INVALID", "Purchase fact is invalid.");
        tenant.Set(fact.TenantId);
        await using var transaction = await enrollmentStore.LockAsync(new(fact.TenantId, Guid.Empty, $"purchase/{fact.OrderId:D}"), cancellationToken);
        if (await db.AccessGrants.AnyAsync(x => x.Origin == "purchase" && x.OriginRef == fact.OrderId, cancellationToken)) return;
        if (!await db.EntitlementCourseViews.AnyAsync(x => x.CourseId == fact.CourseId, cancellationToken))
            throw new EntitlementRuleException("COURSE_UNKNOWN", "Purchase refers to an unknown course.");
        var now = clock.GetUtcNow();
        var enrollment = await enrollmentStore.GetOrCreateEnrollmentAsync(fact.StudentId, fact.CourseId, now, cancellationToken);
        var grant = AccessGrant.CreatePurchase(enrollment, new(fact.OrderId, fact.AccessPeriod.Type, fact.AccessPeriod.Months, now), zone.Zone);
        db.AccessGrants.Add(grant); var eventId = Guid.CreateVersion7();
        var granted = new
        {
            eventId,
            fact.TenantId,
            grantId = grant.Id,
            fact.StudentId,
            fact.CourseId,
            grant.Origin,
            grant.OriginRef,
            fact.AccessPeriod,
            grant.GrantedAt,
            grant.EndsOn,
            grant.ExpiresAt,
            occurredAt = now
        };
        await outbox.AppendAsync(new(eventId, fact.TenantId, "AcessoConcedido", "matricula.acesso-concedido.v1",
         granted, now, Activity.Current?.Id), cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken); await transaction.CompleteAsync(cancellationToken);
        CommerceTelemetry.PaymentAccessLag.Record(Math.Max(0, (now - fact.PaidAt).TotalSeconds));
    }
}
