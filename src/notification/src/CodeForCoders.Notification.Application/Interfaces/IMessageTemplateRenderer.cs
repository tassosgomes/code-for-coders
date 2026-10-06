using CodeForCoders.Notification.Domain.DeliveryRecords;

namespace CodeForCoders.Notification.Application.Interfaces;

public interface IMessageTemplateRenderer
{
    TransactionalEmail Render(
        string model,
        string recipient,
        string? recipientName,
        string link,
        string? recipientRole = null,
        PurchaseReceiptData? receiptData = null);
}
