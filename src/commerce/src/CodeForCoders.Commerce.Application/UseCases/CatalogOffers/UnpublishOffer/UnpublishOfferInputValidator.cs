using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UnpublishOffer;

public sealed class UnpublishOfferInputValidator : AbstractValidator<UnpublishOfferInput>
{
    public UnpublishOfferInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.OfferId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
