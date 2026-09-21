using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Domain.Repositories;
using FluentValidation;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.AcceptNotificationSendRequest;

public sealed class AcceptNotificationSendRequest(
    IDeliveryRecordRepository deliveryRecordRepository,
    IUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IValidator<AcceptNotificationSendRequestInput> validator) : IAcceptNotificationSendRequest
{
    public async Task<AcceptNotificationSendRequestOutput> ExecuteAsync(
        AcceptNotificationSendRequestInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        tenantContext.Set(input.Request.TenantId);

        var existingRecord = await deliveryRecordRepository.GetByRequestIdAsync(
            input.Request.TenantId,
            input.Request.PedidoId,
            cancellationToken);
        if (existingRecord is not null)
        {
            return ToOutput(existingRecord);
        }

        var refusalReason = NotificationSendRequestRules.GetRefusalReason(input.Request);
        var transitionOn = DateTimeOffset.UtcNow;
        if (refusalReason is not null)
        {
            var refusedRecord = DeliveryRecord.CreateRefused(
                input.Request.TenantId,
                input.Request.PedidoId,
                input.Request.Destinatario!,
                input.Request.Dados?.Nome,
                input.Request.Dados?.Link,
                input.Request.Finalidade,
                input.Request.Modelo,
                refusalReason,
                input.Request.SolicitadoEm,
                transitionOn,
                input.CorrelationId);

            await deliveryRecordRepository.AddAsync(refusedRecord, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            NotificationTelemetry.NotificationsRefused.Add(1);

            return ToOutput(refusedRecord);
        }

        var acceptedOn = transitionOn;
        var data = input.Request.Dados!;
        var record = DeliveryRecord.Create(
            input.Request.TenantId,
            input.Request.PedidoId,
            input.Request.Destinatario!,
            data.Nome!,
            data.Link!,
            input.Request.Finalidade!,
            input.Request.Modelo!,
            input.Request.SolicitadoEm,
            acceptedOn,
            input.CorrelationId);

        await deliveryRecordRepository.AddAsync(record, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        NotificationTelemetry.NotificationsAccepted.Add(1);

        return ToOutput(record);
    }

    private static AcceptNotificationSendRequestOutput ToOutput(DeliveryRecord record)
        => new(
            record.Id,
            record.RequestId,
            record.TenantId,
            record.Status,
            record.AcceptedOn,
            record.RefusedOn,
            record.Reason);
}
