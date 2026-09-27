using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordAuditComplement;

public sealed class RecordAuditComplement(
    IAuditRecordWriter recordWriter,
    IUnitOfWork unitOfWork,
    ILogger<RecordAuditComplement> logger) : IAuditComplementRecorder
{
    public async Task<AuditComplementRecordStatus> RecordAsync(
        ComplementoConfirmadoV1 complement,
        CancellationToken cancellationToken)
    {
        if (!IsValid(complement))
        {
            return Reject("invalid-envelope");
        }

        var existing = await recordWriter.FindByConfirmationIdAsync(
            complement.TenantId,
            complement.ConfirmationId,
            cancellationToken);
        if (existing is not null)
        {
            return IsSameMessage(existing, complement)
                ? RecordDuplicate()
                : Reject("confirmation-conflict");
        }

        var original = await recordWriter.FindOriginalAsync(
            complement.TenantId,
            complement.OriginalRecordId,
            cancellationToken);
        if (original is null)
        {
            return Reject("original-not-found");
        }

        var record = AuditRecord.CreateComplement(
            complement.TenantId,
            original.Id,
            complement.ConfirmationId,
            ToStoredPrecision(complement.ConfirmedAt),
            complement.Author.Type,
            complement.Author.Id,
            complement.Explanation);
        await recordWriter.AppendAsync(record, cancellationToken);
        try
        {
            await unitOfWork.CommitAsync(cancellationToken);
            AuditTelemetry.ComplementsRecorded.Add(1);
            return AuditComplementRecordStatus.Recorded;
        }
        catch (AuditRecordAlreadyExistsException)
        {
            var winner = await recordWriter.FindByConfirmationIdAsync(
                complement.TenantId,
                complement.ConfirmationId,
                cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return IsSameMessage(winner, complement)
                ? RecordDuplicate()
                : Reject("confirmation-conflict");
        }
    }

    private AuditComplementRecordStatus RecordDuplicate()
    {
        AuditTelemetry.ComplementsRedelivered.Add(1);
        return AuditComplementRecordStatus.Duplicate;
    }

    private AuditComplementRecordStatus Reject(string reason)
    {
        AuditTelemetry.ComplementsRejected.Add(1);
        logger.LogError("Audit complement message rejected and sent to the dead-letter queue {Reason}", reason);
        return AuditComplementRecordStatus.Rejected;
    }

    private static bool IsValid(ComplementoConfirmadoV1? complement)
        => complement is not null
            && complement.ConfirmationId != Guid.Empty
            && complement.TenantId != Guid.Empty
            && complement.OriginalRecordId != Guid.Empty
            && complement.ConfirmedAt != default
            && complement.Author is not null
            && complement.Author.Id != Guid.Empty
            && !string.IsNullOrWhiteSpace(complement.Author.Type)
            && complement.Author.Type.Length <= AuditRecord.OriginMaxLength
            && !string.IsNullOrWhiteSpace(complement.Explanation)
            && complement.Explanation.Length <= AuditRecord.ReasonMaxLength;

    // PostgreSQL timestamptz keeps microseconds while the producer sends 100 ns ticks; the record and the
    // redelivery comparison both use the stored precision so an identical message is never a conflict.
    private static DateTimeOffset ToStoredPrecision(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
    }

    private static bool IsSameMessage(AuditRecord existing, ComplementoConfirmadoV1 complement)
        => existing.RecordType == AuditRecord.ComplementRecordType
            && existing.OriginalRecordId == complement.OriginalRecordId
            && existing.ConfirmedAt == ToStoredPrecision(complement.ConfirmedAt)
            && existing.AuthorType == complement.Author.Type
            && existing.AuthorId == complement.Author.Id
            && existing.Explanation == complement.Explanation;
}
