using System.Text.Json;
namespace CodeForCoders.BffStudent.Api.Clients;

public static class OrderResponseValidation
{
    public static bool IsValid(JsonElement body, bool summary)
    {
        if (!Object(body, "course", out var course) || !Uuid(course, "courseId") || !Text(course, "title")
            || !Object(body, "offer", out var offer) || !Uuid(offer, "offerId") || !Text(offer, "name")) return false;
        var priced = summary ? offer : body;
        if (!priced.TryGetProperty("priceCents", out var price) || price.ValueKind != JsonValueKind.Number || !price.TryGetInt32(out var cents) || cents < 1
            || !priced.TryGetProperty("currency", out var currency) || currency.ValueKind != JsonValueKind.String || currency.GetString() != "BRL"
            || !Object(priced, "accessPeriod", out var period) || !Text(period, "type")) return false;
        var type = period.GetProperty("type").GetString();
        if (type != "lifetime" && (type != "months" || !period.TryGetProperty("months", out var months) || months.ValueKind != JsonValueKind.Number || !months.TryGetInt32(out var count) || count is < 1 or > 60)) return false;
        if (summary) return NullableUuid(body, "pendingOrderId") && body.TryGetProperty("existingAccessChecked", out var check)
            && check.ValueKind is JsonValueKind.True or JsonValueKind.False && body.TryGetProperty("existingAccess", out var access)
            && (access.ValueKind == JsonValueKind.Null || Object(access, "validity", out _) && Text(access, "origin"));
        return Uuid(body, "orderId") && Text(body, "number") && Text(body, "status") && Text(body, "createdAt")
            && new[] { "paymentMethod", "pendingPayment", "paymentPageExpiresAt", "accessGrantedAt", "paidAt", "expiredAt", "cancelledAt" }.All(name => body.TryGetProperty(name, out _));
    }
    private static bool Object(JsonElement body, string name, out JsonElement value)
    { value = default; return body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object; }
    private static bool Text(JsonElement body, string name) => body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String;
    private static bool Uuid(JsonElement body, string name) => Text(body, name) && Guid.TryParse(body.GetProperty(name).GetString(), out var id) && id != Guid.Empty;
    private static bool NullableUuid(JsonElement body, string name) => body.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.Null || Uuid(body, name));
}
