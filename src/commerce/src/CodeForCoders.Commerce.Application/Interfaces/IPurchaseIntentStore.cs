namespace CodeForCoders.Commerce.Application.Interfaces;

public interface IPurchaseIntentStore
{
    Task<PurchaseIntentResult> RegisterAsync(PurchaseIntentRegistration registration, CancellationToken cancellationToken);
}
