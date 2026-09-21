namespace CodeForCoders.Notification.Application.Interfaces;

public interface IMessageTemplateRenderer
{
    TransactionalEmail Render(
        string model,
        string recipient,
        string recipientName,
        string link);
}
