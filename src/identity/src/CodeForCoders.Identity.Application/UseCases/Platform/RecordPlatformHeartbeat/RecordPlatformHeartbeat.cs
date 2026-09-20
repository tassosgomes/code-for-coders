using System.Diagnostics;
using CodeForCoders.Identity.Application.Common;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Contracts;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Platform.RecordPlatformHeartbeat;

public sealed class RecordPlatformHeartbeat(
    IOutboxMessageWriter outboxMessageWriter,
    IUnitOfWork unitOfWork,
    IValidator<RecordPlatformHeartbeatInput> validator) : IRecordPlatformHeartbeat
{
    public const string EventType = "PlatformHeartbeatV1";
    public const string RoutingKey = "identity.platform.heartbeat.v1";

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
            IdentityTelemetry.ServiceName);

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
        IdentityTelemetry.HeartbeatsRecorded.Add(1);

        return new RecordPlatformHeartbeatOutput(eventId, input.TenantId, occurredOn, RoutingKey);
    }
}
