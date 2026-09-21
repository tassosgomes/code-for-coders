using CodeForCoders.Audit.Application.Common;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Contracts;
using CodeForCoders.Audit.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Audit.Application.UseCases.Audit.RecordConsumedAuditEvent;

/// <summary>
/// Technical consumer use case: validate an incoming event and append it atomically.
/// </summary>
public sealed class RecordConsumedAuditEvent(
    IAuditRecordWriter recordWriter,
    IUnitOfWork unitOfWork,
    IValidator<RecordConsumedAuditEventInput> validator) : IRecordConsumedAuditEvent, IAuditEventRecorder
{
    public async Task RecordAsync(
        AuditEventV1 auditEvent,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(new RecordConsumedAuditEventInput(auditEvent), cancellationToken);
    }

    public async Task<RecordConsumedAuditEventOutput> ExecuteAsync(
        RecordConsumedAuditEventInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var auditEvent = input.Event;
        var recordedOn = DateTimeOffset.UtcNow;
        var record = AuditRecord.Create(
            auditEvent.EventId,
            auditEvent.TenantId,
            auditEvent.SourceService,
            auditEvent.EventType,
            auditEvent.Payload,
            auditEvent.OccurredOn,
            recordedOn);

        await recordWriter.AppendAsync(record, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        AuditTelemetry.EventsRecorded.Add(1);

        return new RecordConsumedAuditEventOutput(auditEvent.EventId, recordedOn);
    }
}
