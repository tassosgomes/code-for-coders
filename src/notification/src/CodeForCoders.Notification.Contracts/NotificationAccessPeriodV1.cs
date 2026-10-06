using System.Text.Json.Serialization;

namespace CodeForCoders.Notification.Contracts;

public sealed record NotificationAccessPeriodV1(string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Months = null);
