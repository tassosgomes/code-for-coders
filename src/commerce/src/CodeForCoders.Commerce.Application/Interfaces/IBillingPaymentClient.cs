namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IBillingPaymentClient { Task<BillingPaymentSession> EnsureAsync(BillingPaymentRequest request, CancellationToken cancellationToken); }
