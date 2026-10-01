namespace CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;

public sealed record RegisterPurchaseIntentInput(Guid OfferId, string IdempotencyKey);
