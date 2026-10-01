using CodeForCoders.Commerce.Application.UseCases.CatalogOffers.Common;
using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogOffers.DeleteOffer;

public sealed class DeleteOfferInputValidator : AbstractValidator<DeleteOfferInput>
{
    public DeleteOfferInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
