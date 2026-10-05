namespace CodeForCoders.Billing.Application.Interfaces;

public interface IPaymentGateway
{
    Task<GatewaySession> OpenAsync(GatewaySessionRequest request, CancellationToken cancellationToken);
    Task<GatewaySession> GetAsync(string reference, CancellationToken cancellationToken);
    Task<string> GetInstructionsUrlAsync(string paymentReference, string method, CancellationToken cancellationToken);
    Task<string> GetPaymentMethodAsync(string paymentReference, CancellationToken cancellationToken);
    GatewayEvent VerifyEvent(string body, string? signature);
}
