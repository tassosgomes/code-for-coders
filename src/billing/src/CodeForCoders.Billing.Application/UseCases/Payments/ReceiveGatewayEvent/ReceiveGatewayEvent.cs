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
        await store.ReceiveAsync(receipt, cancellationToken);
        BillingTelemetry.GatewayEventsReceived.Add(1, new KeyValuePair<string, object?>("type", receipt.Outcome ?? "ignored"));
    }
}
