using FluentValidation;
namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListFinanceOrders;

public sealed class ListFinanceOrdersInputValidator : AbstractValidator<ListFinanceOrdersInput>
{
    public ListFinanceOrdersInputValidator()
    {
        RuleFor(input => input.Status).Must(value => value is null or "awaiting-payment" or "paid" or "expired" or "cancelled");
        RuleFor(input => input.CourseId).NotEqual(Guid.Empty);
        RuleFor(input => input.StudentId).NotEqual(Guid.Empty);
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 50);
        RuleFor(input => input).Must(input => (long)(input.Page - 1) * input.Size <= int.MaxValue).WithName("_page");
        RuleFor(input => input).Must(input => input.CreatedFrom is null || input.CreatedTo is null || input.CreatedFrom <= input.CreatedTo)
            .WithName("createdTo");
        RuleFor(input => input.CreatedTo).Must(value => value != DateOnly.MaxValue);
    }
}
