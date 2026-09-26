using System.Diagnostics;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
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
    IDeliveryOutcomeCounterRepository deliveryOutcomeCounterRepository,
    IUnitOfWork unitOfWork,
    ITransactionalEmailRetryPolicy retryPolicy) : IDeliverAcceptedNotification
{
    public async Task<DeliverAcceptedNotificationOutput> ExecuteAsync(
        DeliverAcceptedNotificationInput input,
        CancellationToken cancellationToken)
    {
        var record = await deliveryRecordRepository.GetAsync(input.DeliveryRecordId, cancellationToken);
        if (record is null
            || record.Status != DeliveryStatus.Accepted
            || record.Recipient is null
            || (record.Model == NotificationPurposes.StaffInvitation
                ? record.RecipientRole is null
                : record.RecipientName is null)
            || record.Link is null
            || record.Purpose is null
            || record.Model is null)
        {
            return new DeliverAcceptedNotificationOutput(false, null, null);
        }

        var recipient = record.Recipient;

        var allowed = await consentService.AllowsAsync(
            recipient,
            record.Purpose,
            cancellationToken);
        if (!allowed)
        {
            return new DeliverAcceptedNotificationOutput(false, null, null);
        }

        var email = messageTemplateRenderer.Render(
            record.Model,
            recipient,
            record.RecipientName,
            record.Link,
            record.RecipientRole);
        var attemptedOn = DateTimeOffset.UtcNow;
        record.RegisterProviderAttempt(attemptedOn);
        try
        {
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch (TransactionalEmailSendException exception)
        {
            return await HandleProviderFailureAsync(
                record,
                exception,
                attemptedOn,
                cancellationToken);
        }

        var deliveredOn = DateTimeOffset.UtcNow;
        record.MarkDelivered(deliveredOn);
        await deliveryOutcomeCounterRepository.IncrementAsync(
            record,
            deliveredOn,
            cancellationToken);
        var eventId = Guid.CreateVersion7();
        var payload = new NotificationMessageDeliveredV1(
            record.RequestId,
            record.TenantId,
            record.Purpose,
            recipient,
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
                record.CorrelationId,
                record.Namespace),
            cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        NotificationTelemetry.NotificationsDelivered.Add(1);

        return new DeliverAcceptedNotificationOutput(true, eventId, deliveredOn);
    }

    private async Task<DeliverAcceptedNotificationOutput> HandleProviderFailureAsync(
        DeliveryRecord record,
        TransactionalEmailSendException exception,
        DateTimeOffset failedOn,
        CancellationToken cancellationToken)
    {
        var exhaustedAttempts = exception.IsTransient
            && record.ProviderAttemptCount >= retryPolicy.MaxAttempts;
        if (!exception.IsTransient || exhaustedAttempts)
        {
            var reason = exhaustedAttempts
                ? NotificationFailureReasons.AttemptsExhausted
                : exception.Reason;
            record.MarkFailed(reason, exhaustedAttempts, failedOn);
            await deliveryOutcomeCounterRepository.IncrementAsync(
                record,
                failedOn,
                cancellationToken);
            var eventId = Guid.CreateVersion7();
            var payload = new NotificationDeliveryFailedV1(
                record.RequestId,
                record.TenantId,
                record.Purpose!,
                reason,
                exhaustedAttempts,
                failedOn);
            await outboxMessageWriter.AppendAsync(
                new OutboxMessageDraft(
                    eventId,
                    record.TenantId,
                    "NotificationDeliveryFailedV1",
                    "notificacao.entrega-falhou.v1",
                    payload,
                    failedOn,
                    Activity.Current?.Id,
                    record.CorrelationId,
                    record.Namespace),
                cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);
            if (exhaustedAttempts)
            {
                NotificationTelemetry.NotificationsManualTreatmentRequired.Add(1);
            }

            return new DeliverAcceptedNotificationOutput(false, eventId, null);
        }

        record.ScheduleRetry(
            exception.Reason,
            failedOn.Add(retryPolicy.GetBackoff(record.ProviderAttemptCount)));
        await unitOfWork.CommitAsync(cancellationToken);
        return new DeliverAcceptedNotificationOutput(false, null, null);
    }
}
