using CodeForCoders.Commerce.Application.Exceptions;
using CodeForCoders.Commerce.Application.Interfaces;
using FluentValidation;
namespace CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;

public sealed class RegisterPurchaseIntent(IPurchaseOfferReader catalog, IValidator<RegisterPurchaseIntentInput> validator) : IRegisterPurchaseIntent
{
    public const string NotFoundCode = "OFFER_NOT_AVAILABLE";
    public async Task<PurchaseIntentAccepted> ExecuteAsync(RegisterPurchaseIntentInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        if (await catalog.FindAsync(input.OfferId, cancellationToken) is null) throw new NotFoundException(NotFoundCode);
        return new("available");
    }
}
