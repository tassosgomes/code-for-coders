namespace CodeForCoders.Notification.Domain.DeliveryRecords;

public sealed record PurchaseReceiptData(string OrderNumber, string Course, string Offer, int AmountCents,
    string PaymentMethod, DateTimeOffset PaidAt, string PeriodType, int? PeriodMonths);
