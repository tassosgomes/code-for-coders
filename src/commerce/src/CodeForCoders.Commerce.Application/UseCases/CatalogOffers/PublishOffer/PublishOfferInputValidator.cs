using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.PublishOffer;

public sealed class PublishOfferInputValidator : AbstractValidator<PublishOfferInput>
{
    public PublishOfferInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.OfferId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
