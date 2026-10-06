using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Sales.ListStudentOrders;

public sealed class ListStudentOrdersInputValidator : AbstractValidator<ListStudentOrdersInput>
{
    public ListStudentOrdersInputValidator()
    {
        RuleFor(input => input.StudentId).NotEmpty();
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 50);
        RuleFor(input => input).Must(input => (long)(input.Page - 1) * input.Size <= int.MaxValue)
            .WithName("_page");
    }
}
