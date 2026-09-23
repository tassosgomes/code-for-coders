using System.Net;
using System.Net.Mail;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Notification.Infra.Data.Adapters;

public sealed class SmtpTransactionalEmailSender(IOptions<EmailOptions> options) : ITransactionalEmailSender
{
    public async Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var message = new MailMessage(settings.FromAddress, email.To)
        {
            Subject = email.Subject,
            Body = email.TextBody,
            IsBodyHtml = false,
        };
        if (!string.IsNullOrWhiteSpace(email.HtmlBody))
        {
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                email.TextBody,
                null,
                "text/plain"));
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                email.HtmlBody,
                null,
                "text/html"));
        }

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.SmtpEnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000,
        };
        if (!string.IsNullOrWhiteSpace(settings.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(settings.SmtpUsername, settings.SmtpPassword);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException exception)
        {
            var statusCode = (int)exception.StatusCode;
            var transient = statusCode is >= 400 and < 500;
            throw new TransactionalEmailSendException(
                transient ? NotificationFailureReasons.ProviderUnavailable : NotificationFailureReasons.PermanentProviderFailure,
                transient,
                exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransactionalEmailSendException(
                NotificationFailureReasons.ProviderTimeout,
                isTransient: true,
                exception);
        }
    }
}
