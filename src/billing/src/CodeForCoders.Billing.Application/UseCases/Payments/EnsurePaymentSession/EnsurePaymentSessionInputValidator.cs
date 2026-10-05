using FluentValidation;
namespace CodeForCoders.Billing.Application.UseCases.Payments.EnsurePaymentSession;

public sealed class EnsurePaymentSessionInputValidator : AbstractValidator<EnsurePaymentSessionInput>
{
    public EnsurePaymentSessionInputValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.AmountCents).InclusiveBetween(1, 9999999); RuleFor(x => x.Currency).Equal("BRL");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(263);
        RuleFor(x => x.SuccessUrl).NotEmpty(); RuleFor(x => x.CancelUrl).NotEmpty();
    }
}
