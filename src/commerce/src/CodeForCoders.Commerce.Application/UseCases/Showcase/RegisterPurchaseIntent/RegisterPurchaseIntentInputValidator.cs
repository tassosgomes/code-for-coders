using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Showcase.RegisterPurchaseIntent;

public sealed class RegisterPurchaseIntentInputValidator : AbstractValidator<RegisterPurchaseIntentInput>
{
    public RegisterPurchaseIntentInputValidator()
    {
        RuleFor(input => input.OfferId).NotEmpty();
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
