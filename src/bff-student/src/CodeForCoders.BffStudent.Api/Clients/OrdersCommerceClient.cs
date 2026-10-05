using System.Net.Http.Json;
using System.Text.Json;
namespace CodeForCoders.BffStudent.Api.Clients;

public sealed class OrdersCommerceClient(HttpClient client) : IOrdersCommerceClient
{
    public async Task<OrderProxyResult> SendAsync(OrderProxyRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(input.Method, input.Path);
        request.Headers.Authorization = new("Bearer", input.AccessToken);
        if (input.OfferId is not null) request.Content = JsonContent.Create(new { offerId = input.OfferId.Value });
        if (input.IdempotencyKey is not null) request.Headers.Add("Idempotency-Key", input.IdempotencyKey);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var body = json.RootElement;
            if (body.ValueKind != JsonValueKind.Object) return new(502, "COMMERCE_UNAVAILABLE");
            var status = (int)response.StatusCode;
            if (status is 200 or 201 && OrderResponseValidation.IsValid(body, input.Path.EndsWith("/purchase-summary", StringComparison.Ordinal)))
                return new(status, Body: body.Clone());
            var code = body.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return (status, code) switch
            {
                (400, "INVALID_REQUEST" or "FIELD_INVALID") => new(400, "VALIDATION_ERROR"),
                (404, "OFFER_NOT_AVAILABLE" or "ORDER_NOT_FOUND") => new(404, code),
                (422, "IDEMPOTENCY_KEY_REUSED") => new(422, code),
                _ => new(502, "COMMERCE_UNAVAILABLE")
            };
        }
        catch (HttpRequestException) { return new(502, "COMMERCE_UNAVAILABLE"); }
        catch (JsonException) { return new(502, "COMMERCE_UNAVAILABLE"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(504, "UPSTREAM_TIMEOUT"); }
    }
}
