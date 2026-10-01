using CodeForCoders.Commerce.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Commerce.Infra.Data.Catalog;

public sealed class PurchaseIntentStore(CommerceDbContext dbContext) : IPurchaseIntentStore
{
    public async Task<PurchaseIntentResult> RegisterAsync(PurchaseIntentRegistration registration, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var (tenant, offer, hash, now) = registration;
        var receiptLock = $"purchase-intent/{tenant:D}/{offer:D}/{hash}";
        // Serializes the same receipt, including expiration and concurrent retry on different days.
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({receiptLock}, 0))", cancellationToken);
        await dbContext.Database.ExecuteSqlAsync($"DELETE FROM catalog.purchase_intent_receipts WHERE tenant_id = {tenant} AND offer_id = {offer} AND key_hash = {hash} AND expires_at <= {now}", cancellationToken);
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var expiresAt = now.AddHours(24);
        // FOR UPDATE orders this operation against unpublication. Availability and increment share one statement.
        var results = await dbContext.Database.SqlQuery<int>($"""
            WITH available AS MATERIALIZED (
                SELECT offer_id FROM catalog.offers
                WHERE tenant_id = {tenant} AND offer_id = {offer} AND status = 'published'
                FOR UPDATE
            ), receipt AS (
                INSERT INTO catalog.purchase_intent_receipts (tenant_id, offer_id, key_hash, expires_at)
                SELECT {tenant}, offer_id, {hash}, {expiresAt} FROM available
                ON CONFLICT (tenant_id, offer_id, key_hash) DO NOTHING
                RETURNING offer_id
            ), counted AS (
                INSERT INTO catalog.purchase_intent_daily_counts (tenant_id, offer_id, day, count)
                SELECT {tenant}, offer_id, {day}, 1 FROM receipt
                ON CONFLICT (tenant_id, offer_id, day) DO UPDATE
                SET count = purchase_intent_daily_counts.count + 1
                RETURNING count
            )
            SELECT CASE WHEN EXISTS (SELECT 1 FROM counted) THEN 2
                WHEN EXISTS (SELECT 1 FROM available) THEN 1 ELSE 0 END AS "Value"
            """).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (PurchaseIntentResult)results.Single();
    }
}
