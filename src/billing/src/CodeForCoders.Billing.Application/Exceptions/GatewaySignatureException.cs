namespace CodeForCoders.Billing.Application.Exceptions;

public sealed class GatewaySignatureException() : Exception("Gateway signature or event is invalid.");
