using System.Diagnostics;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Domain.Entities;
using CodeForCoders.Billing.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CodeForCoders.Billing.Infra.Data.Payments;

public sealed class PaymentStore(BillingDbContext db, ITenantContext tenant, IOutboxMessageWriter outbox, TimeProvider clock) : IPaymentStore
{
    public async Task<IPaymentTransaction> LockAsync(Guid tenantId, Guid orderId, CancellationToken cancellationToken)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var key = $"payment/{tenant.Namespace}/{tenantId:D}/{orderId:D}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            return new PaymentTransaction(transaction);
        }
        catch (OperationCanceledException) { await transaction.DisposeAsync(); throw; }
        catch (NpgsqlException) { await transaction.DisposeAsync(); throw; }
    }
    public Task<Payment?> FindAsync(Guid orderId, CancellationToken cancellationToken)
     => db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
    public void Add(Payment payment) => db.Payments.Add(payment);
    public async Task ReceiveAsync(GatewayEvent receipt, CancellationToken cancellationToken)
    {
        var entry = GatewayInboxEntry.Create(receipt, tenant.Namespace);
        db.GatewayInboxEntries.Add(entry);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.Entry(entry).State = EntityState.Detached; }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException) { throw new GatewayInboxUnavailableException(); }
    }
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var entries = await db.GatewayInboxEntries.FromSqlInterpolated(
         $"SELECT * FROM billing_access.gateway_inbox WHERE namespace = {tenant.Namespace} AND processed_at IS NULL ORDER BY occurred_at LIMIT 50 FOR UPDATE SKIP LOCKED")
         .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            if (entry.Outcome != "confirmed") { entry.MarkProcessed(clock.GetUtcNow()); continue; }
            if (entry.TenantId is not { } school || entry.OrderId is not { } orderId || entry.AmountCents is not > 0
             || entry.Currency != "BRL" || string.IsNullOrEmpty(entry.PaymentReference))
            { entry.MarkProcessed(clock.GetUtcNow()); continue; }
            tenant.Set(school);
            var key = $"payment/{tenant.Namespace}/{school:D}/{orderId:D}";
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            var payment = await FindAsync(orderId, cancellationToken);
            // A webhook may beat the creation transaction. Leave it durable for the next cycle.
            if (payment is null) continue;
            if (payment.SessionReference != entry.SessionReference) { entry.MarkProcessed(clock.GetUtcNow()); continue; }
            if (payment.Confirm(entry.PaymentReference, entry.OccurredAt))
            {
                var eventId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
                var fact = new PaymentConfirmedV1(eventId, school, payment.Id, orderId, entry.Method!, entry.AmountCents.Value,
                 entry.Currency, entry.PaymentReference, entry.OccurredAt, now);
                await outbox.AppendAsync(new(eventId, school, "PagamentoConfirmado", "cobranca.pagamento-confirmado.v1", fact,
                 now, Activity.Current?.Id, Activity.Current?.Id ?? eventId.ToString("D")), cancellationToken);
            }
            entry.MarkProcessed(clock.GetUtcNow());
        }
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return entries.Count;
    }
}
