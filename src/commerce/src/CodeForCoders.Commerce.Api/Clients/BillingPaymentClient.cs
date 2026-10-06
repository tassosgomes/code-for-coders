using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using CodeForCoders.Commerce.Domain.Entities;
namespace CodeForCoders.Commerce.Api.Clients;

public sealed class BillingPaymentClient(HttpClient client, BillingAssertionTokenFactory assertions) : IBillingPaymentClient
{
    public async Task<BillingPaymentSession> EnsureAsync(BillingPaymentRequest input, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"internal/v1/payment-sessions/{input.OrderId:D}")
        { Content = JsonContent.Create(new { input.StudentId, input.AmountCents, input.Currency, input.Description, input.SuccessUrl, input.CancelUrl }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertions.Create(input.TenantId));
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            {
                try
                {
                    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    var code = doc.RootElement.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
                    if (code is "PAYMENT_EXPIRED" or "PAYMENT_CANCELLED")
                    {
                        var detail = doc.RootElement.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : null;
                        throw new OrderRuleException("ORDER_NOT_PAYABLE", detail ?? "O prazo de pagamento venceu; o pedido não pode ser pago.");
                    }
                }
                catch (JsonException) { }
            }
            if (!response.IsSuccessStatusCode) throw new PaymentProviderUnavailableException();
            var result = await response.Content.ReadFromJsonAsync<BillingPaymentSession>(cancellationToken);
            if (result is null || result.OrderId != input.OrderId || result.Kind != "checkout" || result.Method is not null
             || !Uri.TryCreate(result.PaymentUrl, UriKind.Absolute, out var url) || url.Scheme != "https" || result.ExpiresAt == default)
                throw new PaymentProviderUnavailableException();
            return result;
        }
        catch (JsonException) { throw new PaymentProviderUnavailableException(); }
        catch (HttpRequestException) { throw new PaymentProviderUnavailableException(); }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new PaymentProviderUnavailableException(); }
    }
}
