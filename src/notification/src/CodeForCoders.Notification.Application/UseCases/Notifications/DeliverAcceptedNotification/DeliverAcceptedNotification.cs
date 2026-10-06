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
    ITransactionalEmailRetryPolicy retryPolicy,
    IStudentContactClient studentContacts) : IDeliverAcceptedNotification
{
    public async Task<DeliverAcceptedNotificationOutput> ExecuteAsync(
        DeliverAcceptedNotificationInput input,
        CancellationToken cancellationToken)
    {
        var record = await deliveryRecordRepository.GetAsync(input.DeliveryRecordId, cancellationToken);
        if (record is null
            || record.Status != DeliveryStatus.Accepted
            || record.Link is null
            || record.Purpose is null
            || record.Model is null)
        {
            return new DeliverAcceptedNotificationOutput(false, null, null);
        }

        var attemptedOn = DateTimeOffset.UtcNow;
        try
        {
            if (record.RecipientAccountId is { } studentId)
            {
                // Re-resolve on every attempt, including retries after provider failures.
                record.RegisterProviderAttempt(attemptedOn);
                var contact = await studentContacts.GetAsync(record.TenantId, studentId, cancellationToken);
                if (contact is null || contact.Status != "active")
                    throw new TransactionalEmailSendException(NotificationFailureReasons.RecipientUnavailable, false);
                record.ResolveContact(contact.Email, contact.Name);
            }
            if (record.Recipient is null || (record.RecipientName is null && record.RecipientRole is null))
                return new DeliverAcceptedNotificationOutput(false, null, null);
            if (!await consentService.AllowsAsync(record.Recipient, record.Purpose, cancellationToken))
                return new DeliverAcceptedNotificationOutput(false, null, null);
            var email = messageTemplateRenderer.Render(record.Model, record.Recipient, record.RecipientName,
                record.Link, record.RecipientRole, record.ReceiptData);
            if (record.RecipientAccountId is null) record.RegisterProviderAttempt(attemptedOn);
            await emailSender.SendAsync(email, cancellationToken);
        }
        catch (TransactionalEmailSendException exception)
        {
            return await HandleProviderFailureAsync(record, exception, attemptedOn, cancellationToken);
        }

        var recipient = record.Recipient!;
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
