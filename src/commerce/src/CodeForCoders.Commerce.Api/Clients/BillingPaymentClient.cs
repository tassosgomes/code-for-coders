using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CodeForCoders.Commerce.Api.Security;
using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
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
