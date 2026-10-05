using CodeForCoders.Billing.Application.Common;
using CodeForCoders.Billing.Application.Exceptions;
using CodeForCoders.Billing.Application.Interfaces;
namespace CodeForCoders.Billing.Application.UseCases.Payments.ReceiveGatewayEvent;

public sealed class ReceiveGatewayEvent(IPaymentGateway gateway, IPaymentStore store) : IReceiveGatewayEvent
{
    public async Task ExecuteAsync(ReceiveGatewayEventInput input, CancellationToken cancellationToken)
    {
        GatewayEvent receipt;
        try { receipt = gateway.VerifyEvent(input.Body, input.Signature); }
        catch (GatewaySignatureException)
        {
            BillingTelemetry.GatewaySignaturesRefused.Add(1);
            throw;
        }
        // Awaiting and async confirmation carry no method; ask the gateway for the one the payer actually used.
        if (receipt.Method is null && receipt.Outcome is not null && !string.IsNullOrEmpty(receipt.PaymentReference))
            receipt = receipt with { Method = await gateway.GetPaymentMethodAsync(receipt.PaymentReference, cancellationToken) };
        await store.ReceiveAsync(receipt, cancellationToken);
        BillingTelemetry.GatewayEventsReceived.Add(1, new KeyValuePair<string, object?>("type", receipt.Outcome ?? "ignored"));
    }
}
