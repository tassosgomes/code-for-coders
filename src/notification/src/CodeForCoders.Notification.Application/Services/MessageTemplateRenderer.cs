using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Domain.SeedWork;

namespace CodeForCoders.Notification.Application.Services;

public sealed class MessageTemplateRenderer(IEmailTemplateSettings settings) : IMessageTemplateRenderer
{
    public TransactionalEmail Render(
        string model,
        string recipient,
        string recipientName,
        string link)
    {
        if (model != NotificationPurposes.AccountConfirmation)
        {
            throw new EntityValidationException("The notification model is not supported by this slice.");
        }

        var validityHours = settings.GetLinkValidityHours(NotificationPurposes.AccountConfirmation);
        var validityText = validityHours == 1
            ? "1 hour"
            : $"{validityHours} hours";
        var textBody = $"Hello {recipientName},\n\n"
            + $"Confirm your account by opening this link:\n{link}\n\n"
            + $"This link is valid for {validityText}.";

        return new TransactionalEmail(
            recipient,
            "Confirm your account",
            textBody);
    }
}
