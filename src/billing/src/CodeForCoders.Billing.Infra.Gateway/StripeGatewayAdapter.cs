using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
using CodeForCoders.Billing.Infra.Gateway.Configuration;
using Microsoft.Extensions.Options;
namespace CodeForCoders.Billing.Infra.Gateway;

public sealed class StripeGatewayAdapter(HttpClient client, IOptions<StripeGatewayOptions> options, TimeProvider clock) : IPaymentGateway
{
    public async Task<GatewaySession> OpenAsync(GatewaySessionRequest input, CancellationToken cancellationToken)
    {
        var fields = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["client_reference_id"] = input.OrderId.ToString("D"),
            ["metadata[tenantId]"] = input.TenantId.ToString("D"),
            ["metadata[orderId]"] = input.OrderId.ToString("D"),
            ["payment_intent_data[metadata][tenantId]"] = input.TenantId.ToString("D"),
            ["payment_intent_data[metadata][orderId]"] = input.OrderId.ToString("D"),
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = input.Terms.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = input.Terms.AmountCents.ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = input.Terms.Description,
            ["payment_method_types[0]"] = "card",
            ["payment_method_types[1]"] = "pix",
            ["payment_method_types[2]"] = "boleto",
            ["success_url"] = input.SuccessUrl,
            ["cancel_url"] = input.CancelUrl
        };
        // Checkout defaults to 24 hours. Omitting a moving expires_at keeps retries identical.
        using var request = Request(HttpMethod.Post, "v1/checkout/sessions");
        request.Headers.Add("Idempotency-Key", $"payment/{input.TenantId:D}/{input.OrderId:D}");
        request.Content = new FormUrlEncodedContent(fields);
        return await SendSessionAsync(request, cancellationToken);
    }
    public async Task<GatewaySession> GetAsync(string reference, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, $"v1/checkout/sessions/{Uri.EscapeDataString(reference)}");
        return await SendSessionAsync(request, cancellationToken);
    }
    public GatewayEvent VerifyEvent(string body, string? signature)
     => StripeEventTranslator.Verify(body, signature, options.Value.WebhookSigningSecret, clock.GetUtcNow());
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SecretKey)) throw new GatewayUnavailableException();
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.SecretKey); return request;
    }
    private async Task<GatewaySession> SendSessionAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new GatewayUnavailableException();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String
             || !root.TryGetProperty("url", out var url) || url.ValueKind != JsonValueKind.String
             || !Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")
             || !root.TryGetProperty("expires_at", out var expires) || !expires.TryGetInt64(out var timestamp))
                throw new GatewayUnavailableException();
            return new(id.GetString()!, url.GetString()!, DateTimeOffset.FromUnixTimeSeconds(timestamp));
        }
        catch (HttpRequestException) { throw new GatewayUnavailableException(); }
        catch (JsonException) { throw new GatewayUnavailableException(); }
        catch (ArgumentOutOfRangeException) { throw new GatewayUnavailableException(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new GatewayUnavailableException(); }
    }
}
