using System.Text.Json;
namespace CodeForCoders.BffStudent.Api.Clients;

public static class PaymentResponseValidation
{
    public static bool IsValid(JsonElement body, string path)
        => body.EnumerateObject().Count() == 4
        && body.TryGetProperty("orderId", out var order) && order.ValueKind == JsonValueKind.String && order.TryGetGuid(out var orderId)
        && path == $"internal/v1/orders/{orderId:D}/payment-session"
        && body.TryGetProperty("kind", out var kind) && kind.GetString() is "checkout" or "pix-instructions" or "boleto-instructions"
        && body.TryGetProperty("paymentUrl", out var url) && url.ValueKind == JsonValueKind.String
        && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) && (uri.Scheme == "https" || uri.Scheme == "http")
        && body.TryGetProperty("expiresAt", out var expires) && expires.ValueKind == JsonValueKind.String && expires.TryGetDateTimeOffset(out _);
}
