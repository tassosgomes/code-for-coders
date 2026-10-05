namespace CodeForCoders.Billing.Application.UseCases.Payments.ReceiveGatewayEvent;

public sealed record ReceiveGatewayEventInput(string Body, string? Signature);
