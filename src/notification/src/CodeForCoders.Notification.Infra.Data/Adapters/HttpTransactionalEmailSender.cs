using System.Net.Http.Json;
using CodeForCoders.Notification.Application.Interfaces;
using CodeForCoders.Notification.Infra.Data.Configuration;
using Microsoft.Extensions.Options;

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
        var settings = options.Value;
        using var response = await httpClient.PostAsJsonAsync(
                settings.Endpoint,
                new EmailProviderRequest(
                    settings.FromAddress,
                    email.To,
                    email.Subject,
                    email.TextBody,
                    email.HtmlBody),
                cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private sealed record EmailProviderRequest(
        string From,
        string To,
        string Subject,
        string Text,
        string? Html);
}
