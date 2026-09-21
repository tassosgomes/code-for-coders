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
        return model switch
        {
            NotificationPurposes.AccountConfirmation => RenderAccountConfirmation(
                recipient,
                recipientName,
                link),
            NotificationPurposes.PasswordRecovery => RenderPasswordRecovery(
                recipient,
                recipientName,
                link),
            _ => throw new EntityValidationException(
                "The notification model is not supported by this slice."),
        };
    }

    private TransactionalEmail RenderAccountConfirmation(
        string recipient,
        string recipientName,
        string link)
    {
        var validityText = GetValidityText(NotificationPurposes.AccountConfirmation);
        var textBody = $"Hello {recipientName},\n\n"
            + $"Confirm your account by opening this link:\n{link}\n\n"
            + $"This link is valid for {validityText}.";

        return new TransactionalEmail(
            recipient,
            "Confirm your account",
            textBody);
    }

    private TransactionalEmail RenderPasswordRecovery(
        string recipient,
        string recipientName,
        string link)
    {
        var validityText = GetValidityText(NotificationPurposes.PasswordRecovery);
        var textBody = $"Hello {recipientName},\n\n"
            + $"Reset your password by opening this link:\n{link}\n\n"
            + $"This link is valid for {validityText}.";

        return new TransactionalEmail(
            recipient,
            "Reset your password",
            textBody);
    }

    private string GetValidityText(string purpose)
    {
        var validityHours = settings.GetLinkValidityHours(purpose);
        var validityText = validityHours == 1
            ? "1 hour"
            : $"{validityHours} hours";
        return validityText;
    }
}
