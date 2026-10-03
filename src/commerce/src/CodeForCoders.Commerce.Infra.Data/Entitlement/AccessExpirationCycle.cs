using CodeForCoders.Commerce.Application.Common;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Infra.Data.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Commerce.Infra.Data.Entitlement;

public sealed class AccessExpirationCycle(CommerceDbContext db, IEntitlementOutboxMessageWriter outbox,
    TimeProvider clock, IOptions<AccessExpirationOptions> options, ILogger<AccessExpirationCycle> logger)
{
    public static readonly EventId LagAlert = new(1800, "AccessExpirationLag");
    private static readonly TimeSpan LagAlertThreshold = TimeSpan.FromMinutes(30);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await RecordLagAsync(now, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // This maintenance job spans tenants; every fact retains its grant's tenant.
        var grants = await db.AccessGrants.FromSql($"""
            SELECT * FROM entitlement.access_grants
            WHERE expires_at <= {now} AND expiry_published_at IS NULL
            ORDER BY expires_at, id
            LIMIT {options.Value.BatchSize}
            FOR UPDATE SKIP LOCKED
            """).IgnoreQueryFilters().ToListAsync(cancellationToken);
        var correlationId = $"access-expiration-{Guid.CreateVersion7():D}";
        foreach (var grant in grants)
        {
            var eventId = Guid.CreateVersion7();
            var fact = new
            {
                eventId,
                tenantId = grant.TenantId,
                grantId = grant.Id,
                studentId = grant.StudentId,
                courseId = grant.CourseId,
                origin = grant.Origin,
                expiresAt = grant.ExpiresAt!.Value,
                occurredAt = now
            };
            await outbox.AppendAsync(new(eventId, grant.TenantId, "AcessoExpirado", "matricula.acesso-expirado.v1",
                fact, now, correlationId), cancellationToken);
            grant.MarkExpiryFact(eventId, now);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return grants.Count;
    }

    private async Task RecordLagAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var oldest = await db.AccessGrants.IgnoreQueryFilters()
            .Where(grant => grant.ExpiresAt <= now && grant.ExpiryPublishedAt == null)
            .MinAsync(grant => grant.ExpiresAt, cancellationToken);
        var lag = oldest.HasValue ? now - oldest.Value : TimeSpan.Zero;
        CommerceTelemetry.AccessExpirationLag.Record(lag.TotalSeconds);
        if (lag > LagAlertThreshold)
            logger.LogWarning(LagAlert, "Access expiration facts are overdue. Oldest pending lag is {LagSeconds} seconds.", lag.TotalSeconds);
    }
}
