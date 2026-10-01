using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.UpdateOffer;

public sealed class UpdateOfferInputValidator : AbstractValidator<UpdateOfferInput>
{
    public UpdateOfferInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(input => input.Body).Must(body => OfferRequest.HasValidShape(body, false));
    }
}
