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

        var acceptedOn = DateTimeOffset.UtcNow;
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

        return new AcceptNotificationSendRequestOutput(
            record.Id,
            record.RequestId,
            record.TenantId,
            record.AcceptedOn);
    }
}
