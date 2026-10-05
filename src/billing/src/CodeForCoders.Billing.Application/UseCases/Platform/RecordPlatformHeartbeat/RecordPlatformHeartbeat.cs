using System.Diagnostics;
using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Contracts;
using FluentValidation;

namespace CodeForCoders.Billing.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed class RecordPlatformHeartbeat(
    IOutboxMessageWriter outboxMessageWriter,
    IUnitOfWork unitOfWork,
    IValidator<RecordPlatformHeartbeatInput> validator) : IRecordPlatformHeartbeat
{
    public const string EventType = "PlatformHeartbeatV1";
    public const string RoutingKey = "billing.platform.heartbeat.v1";

    public async Task<RecordPlatformHeartbeatOutput> ExecuteAsync(
        RecordPlatformHeartbeatInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var eventId = Guid.CreateVersion7();
        var occurredOn = DateTimeOffset.UtcNow;
        var payload = new PlatformHeartbeatV1(
            eventId,
            input.TenantId,
            occurredOn,
            BillingTelemetry.ServiceName);

        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                input.TenantId,
                EventType,
                RoutingKey,
                payload,
                occurredOn,
                Activity.Current?.Id),
            cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);
        BillingTelemetry.HeartbeatsRecorded.Add(1);

        return new RecordPlatformHeartbeatOutput(eventId, input.TenantId, occurredOn, RoutingKey);
    }
}
