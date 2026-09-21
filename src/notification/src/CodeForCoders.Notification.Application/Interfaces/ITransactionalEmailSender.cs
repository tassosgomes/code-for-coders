namespace CodeForCoders.Notification.Application.Interfaces;

public interface ITransactionalEmailSender
{
    Task SendAsync(TransactionalEmail email, CancellationToken cancellationToken);
}

public sealed record TransactionalEmail(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null);

public static class NotificationChannels
{
    public const string Email = "email";

    public static IReadOnlyList<string> All { get; } = [Email];
}
