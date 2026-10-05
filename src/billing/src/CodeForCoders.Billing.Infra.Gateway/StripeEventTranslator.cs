using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
namespace CodeForCoders.Billing.Infra.Gateway;

public static class StripeEventTranslator
{
    private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
    public static GatewayEvent Verify(string body, string? signature, string secret, DateTimeOffset now)
    {
        VerifySignature(body, signature, secret, now);
        try
        {
            using var document = JsonDocument.Parse(body); var root = document.RootElement;
            var id = Text(root, "id"); var type = Text(root, "type");
            if (string.IsNullOrEmpty(id) || id.Length > 255 || string.IsNullOrEmpty(type) || type.Length > 255
             || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
             || !data.TryGetProperty("object", out var item) || item.ValueKind != JsonValueKind.Object)
                throw new GatewaySignatureException();
            var occurred = root.TryGetProperty("created", out var created) && created.TryGetInt64(out var timestamp)
             ? DateTimeOffset.FromUnixTimeSeconds(timestamp) : now;
            var objectReference = Text(item, "id") ?? "";
            var tenant = GuidValue(item, "metadata", "tenantId");
            var order = GuidValue(item, "metadata", "orderId");
            if (order is null && Guid.TryParse(Text(item, "client_reference_id"), out var clientOrder)) order = clientOrder;
            var isSession = type.StartsWith("checkout.session.", StringComparison.Ordinal);
            var payment = Text(item, "payment_intent");
            var outcome = type == "checkout.session.completed" && Text(item, "payment_status") == "paid" ? "confirmed" : null;
            // A paid, synchronous Checkout completion is a card payment. Pending asynchronous means arrive in V-03.
            var method = outcome == "confirmed" ? "card" : null;
            int? amount = item.TryGetProperty("amount_total", out var amountValue) && amountValue.TryGetInt32(out var cents) ? cents : null;
            return new(id, type, occurred, objectReference, isSession ? objectReference : null, payment, tenant, order,
             outcome, method, amount, Text(item, "currency")?.ToUpperInvariant());
        }
        catch (JsonException) { throw new GatewaySignatureException(); }
        catch (ArgumentOutOfRangeException) { throw new GatewaySignatureException(); }
    }
    private static void VerifySignature(string body, string? signature, string secret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrEmpty(secret)) throw new GatewaySignatureException();
        var parts = signature.Split(',').Select(x => x.Trim().Split('=', 2)).Where(x => x.Length == 2).ToArray();
        var timestamp = parts.FirstOrDefault(x => x[0] == "t")?[1];
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var time)
          || Math.Abs(now.ToUnixTimeSeconds() - (double)time) > SignatureTolerance.TotalSeconds) throw new GatewaySignatureException();
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        foreach (var part in parts.Where(x => x[0] == "v1"))
        {
            try { if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(part[1]))) return; }
            catch (FormatException) { }
        }
        throw new GatewaySignatureException();
    }
    private static string? Text(JsonElement root, string key) => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static Guid? GuidValue(JsonElement root, string parent, string key)
     => root.TryGetProperty(parent, out var metadata) && metadata.ValueKind == JsonValueKind.Object
      && Guid.TryParse(Text(metadata, key), out var id) && id != Guid.Empty ? id : null;
}
