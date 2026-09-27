using System.Diagnostics;
using CodeForCoders.BffAdmin.Application.Interfaces;
using CodeForCoders.BffAdmin.Contracts;
using FluentValidation;

namespace CodeForCoders.BffAdmin.Application.UseCases.Audit.ConfirmAuditRecordComplement;

public sealed class ConfirmAuditRecordComplement(
    IAuditComplementConfirmationStore store,
    IValidator<ConfirmAuditRecordComplementInput> validator,
    TimeProvider timeProvider) : IConfirmAuditRecordComplement
{
    public async Task<ConfirmAuditRecordComplementOutput> ExecuteAsync(
        ConfirmAuditRecordComplementInput input,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
        {
            return new(ConfirmAuditRecordComplementStatus.InvalidExplanation, null);
        }

        var result = await store.ConfirmAsync(CreateDraft(input), cancellationToken);

        return MapResult(result);
    }

    public async Task<ConfirmAuditRecordComplementOutput?> TryReplayAsync(
        ConfirmAuditRecordComplementInput input,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
        {
            return new(ConfirmAuditRecordComplementStatus.InvalidExplanation, null);
        }

        var existing = await store.FindExistingAsync(CreateDraft(input), cancellationToken);
        return existing is null ? null : MapResult(existing);
    }

    private AuditComplementConfirmationDraft CreateDraft(ConfirmAuditRecordComplementInput input)
    {
        var confirmedAt = timeProvider.GetUtcNow();
        return new AuditComplementConfirmationDraft(
            input.TenantId,
            input.ActorId,
            input.RecordId,
            input.IdempotencyKey,
            Guid.CreateVersion7(confirmedAt),
            input.Explanation!,
            confirmedAt,
            Activity.Current?.Id);
    }

    private static ConfirmAuditRecordComplementOutput MapResult(AuditComplementConfirmationWriteResult result)
        => result.Status switch
        {
            AuditComplementConfirmationWriteStatus.Conflict => new(ConfirmAuditRecordComplementStatus.Conflict, null),
            _ => new(ConfirmAuditRecordComplementStatus.Accepted, result.ConfirmationId),
        };
}
