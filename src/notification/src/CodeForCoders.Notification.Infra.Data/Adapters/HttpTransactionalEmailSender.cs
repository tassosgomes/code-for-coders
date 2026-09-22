using System.Net;
using System.Net.Http.Json;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Application.Common;
using CodeForCoders.Notification.Application.Exceptions;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;

namespace CodeForCoders.Notification.Infra.Data.Adapters;

/// <summary>
/// The foundation keeps one outbound notification channel: transactional email.
/// Provider-specific authentication and payload changes stay behind this adapter.
/// </summary>
public sealed class HttpTransactionalEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options) : ITransactionalEmailSender
{
    public async Task SendAsync(
        TransactionalEmail email,
        CancellationToken cancellationToken)
    {
        await SendRequestAsync(email, options.Value, cancellationToken);
    }

    private async Task SendRequestAsync(
        TransactionalEmail email,
        EmailOptions settings,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                settings.Endpoint,
                new EmailProviderRequest(
                    settings.FromAddress,
                    email.To,
                    email.Subject,
                    email.TextBody,
                    email.HtmlBody),
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var isTransient = response.StatusCode == HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500;
            var reason = isTransient
                ? NotificationFailureReasons.ProviderUnavailable
                : NotificationFailureReasons.PermanentProviderFailure;
            throw new TransactionalEmailSendException(reason, isTransient);
        }
        catch (HttpRequestException exception)
        {
            throw new TransactionalEmailSendException(
                NotificationFailureReasons.ProviderUnavailable,
                isTransient: true,
                exception);
        }
        catch (TimeoutRejectedException exception)
        {
            throw new TransactionalEmailSendException(
                NotificationFailureReasons.ProviderTimeout,
                isTransient: true,
                exception);
        }
        catch (ExecutionRejectedException exception)
        {
            throw new TransactionalEmailSendException(
                NotificationFailureReasons.ProviderUnavailable,
                isTransient: true,
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

    private sealed record EmailProviderRequest(
        string From,
        string To,
        string Subject,
        string Text,
        string? Html);
}
