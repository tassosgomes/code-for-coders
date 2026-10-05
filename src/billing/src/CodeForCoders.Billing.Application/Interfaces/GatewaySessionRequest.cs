using CodeForCoders.Billing.Domain.Entities;
namespace CodeForCoders.Billing.Application.Interfaces;

public sealed record GatewaySessionRequest(Guid TenantId, Guid OrderId, PaymentTerms Terms, string SuccessUrl, string CancelUrl);
