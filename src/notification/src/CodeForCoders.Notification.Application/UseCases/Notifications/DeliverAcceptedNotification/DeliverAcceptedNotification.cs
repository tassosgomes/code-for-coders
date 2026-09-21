using System.Diagnostics;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Contracts;
using CodeForCoders.Notification.Domain.DeliveryRecords;
using CodeForCoders.Notification.Domain.Repositories;

namespace CodeForCoders.Notification.Application.UseCases.Notifications.DeliverAcceptedNotification;

public sealed class DeliverAcceptedNotification(
    IDeliveryRecordRepository deliveryRecordRepository,
    IConsentService consentService,
    IMessageTemplateRenderer messageTemplateRenderer,
    ITransactionalEmailSender emailSender,
    IOutboxMessageWriter outboxMessageWriter,
    IUnitOfWork unitOfWork) : IDeliverAcceptedNotification
{
    public async Task<DeliverAcceptedNotificationOutput> ExecuteAsync(
        DeliverAcceptedNotificationInput input,
        CancellationToken cancellationToken)
    {
        var record = await deliveryRecordRepository.GetAsync(input.DeliveryRecordId, cancellationToken);
        if (record is null || record.Status != DeliveryStatus.Accepted)
        {
            return new DeliverAcceptedNotificationOutput(false, null, null);
        }

        var allowed = await consentService.AllowsAsync(
            record.Recipient,
            record.Purpose!,
            cancellationToken);
        if (!allowed)
        {
            return new DeliverAcceptedNotificationOutput(false, null, null);
        }

        var email = messageTemplateRenderer.Render(
            record.Model!,
            record.Recipient,
            record.RecipientName!,
            record.Link!);
        await emailSender.SendAsync(email, cancellationToken);

        var deliveredOn = DateTimeOffset.UtcNow;
        record.MarkDelivered(deliveredOn);
        var eventId = Guid.CreateVersion7();
        var payload = new NotificationMessageDeliveredV1(
            record.RequestId,
            record.TenantId,
            record.Purpose!,
            record.Recipient,
            NotificationChannels.Email,
            deliveredOn);

        await outboxMessageWriter.AppendAsync(
            new OutboxMessageDraft(
                eventId,
                record.TenantId,
                "NotificationMessageDeliveredV1",
                "notificacao.mensagem-entregue.v1",
                payload,
                deliveredOn,
                Activity.Current?.Id,
                record.CorrelationId),
            cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        NotificationTelemetry.NotificationsDelivered.Add(1);

        return new DeliverAcceptedNotificationOutput(true, eventId, deliveredOn);
    }
}
