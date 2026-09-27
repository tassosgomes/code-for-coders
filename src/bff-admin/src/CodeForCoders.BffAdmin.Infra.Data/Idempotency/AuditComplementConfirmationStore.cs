using System.Security.Cryptography;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Contracts;
using CodeForCoders.BffAdmin.Infra.Data.Outbox;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.BffAdmin.Infra.Data.Idempotency;

public sealed class AuditComplementConfirmationStore(
    BffAdminDbContext dbContext,
    IOutboxMessageWriter outboxMessageWriter,
    OutboxPayloadProtector payloadProtector) : IAuditComplementConfirmationStore
{
    private const string EventType = "ComplementoConfirmado";
    private const string RoutingKey = "auditoria.registro.complemento-confirmado.v1";
    private const string DestinationExchange = "audit.events";

    public async Task<AuditComplementConfirmationWriteResult?> FindExistingAsync(
        AuditComplementConfirmationDraft draft,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.AuditComplementIdempotencyRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.TenantId == draft.TenantId
                && record.ActorId == draft.ActorId
                && record.OperationId == AuditComplementIdempotencyRecord.Operation
                && record.IdempotencyKey == draft.IdempotencyKey,
                cancellationToken);
        if (existing is null || existing.IsExpired(draft.ConfirmedAt))
        {
            return null;
        }

        return Compare(existing, payloadProtector.ComputeFingerprint(draft.RecordId, draft.Explanation));
    }

    public async Task<AuditComplementConfirmationWriteResult> ConfirmAsync(
        AuditComplementConfirmationDraft draft,
        CancellationToken cancellationToken)
    {
        var requestHash = payloadProtector.ComputeFingerprint(draft.RecordId, draft.Explanation);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"{draft.TenantId:D}|{draft.ActorId:D}|{AuditComplementIdempotencyRecord.Operation}|{draft.IdempotencyKey:D}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var existing = await FindAsync(draft, cancellationToken);
        if (existing is not null && !existing.IsExpired(draft.ConfirmedAt))
        {
            await transaction.CommitAsync(cancellationToken);
            return Compare(existing, requestHash);
        }

        if (existing is null)
        {
            dbContext.AuditComplementIdempotencyRecords.Add(AuditComplementIdempotencyRecord.Create(
                draft.TenantId,
                draft.ActorId,
                draft.IdempotencyKey,
                requestHash,
                draft.ConfirmationId,
                draft.ConfirmedAt));
        }
        else
        {
            existing.Refresh(requestHash, draft.ConfirmationId, draft.ConfirmedAt);
        }

        var payload = new ComplementoConfirmadoV1(
            draft.ConfirmationId,
            draft.TenantId,
            draft.RecordId,
            draft.ConfirmedAt,
            new ComplementAuthorV1("conta-interna", draft.ActorId),
            draft.Explanation);
        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                draft.ConfirmationId,
                draft.TenantId,
                EventType,
                RoutingKey,
                payload,
                draft.ConfirmedAt,
                draft.TraceParent,
                DestinationExchange,
                ProtectPayload: true),
            cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(AuditComplementConfirmationWriteStatus.Accepted, draft.ConfirmationId);
        }
        catch (DbUpdateException exception) when (IsIdempotencyKeyConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            var winner = await FindAsync(draft, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return Compare(winner, requestHash);
        }
    }

    private Task<AuditComplementIdempotencyRecord?> FindAsync(
        AuditComplementConfirmationDraft draft,
        CancellationToken cancellationToken)
        => dbContext.AuditComplementIdempotencyRecords
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(record => record.TenantId == draft.TenantId
                && record.ActorId == draft.ActorId
                && record.OperationId == AuditComplementIdempotencyRecord.Operation
                && record.IdempotencyKey == draft.IdempotencyKey,
                cancellationToken);

    private static AuditComplementConfirmationWriteResult Compare(
        AuditComplementIdempotencyRecord existing,
        byte[] requestHash)
        => CryptographicOperations.FixedTimeEquals(existing.RequestHash, requestHash)
            ? new(AuditComplementConfirmationWriteStatus.Replay, existing.ConfirmationId)
            : new(AuditComplementConfirmationWriteStatus.Conflict, Guid.Empty);

    private static bool IsIdempotencyKeyConflict(DbUpdateException exception)
        => exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_audit_complement_idempotency_scope_key",
        };
}
