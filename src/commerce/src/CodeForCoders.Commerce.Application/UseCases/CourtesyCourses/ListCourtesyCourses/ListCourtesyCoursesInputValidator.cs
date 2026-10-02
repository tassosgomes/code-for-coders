using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyCourses.ListCourtesyCourses;

public sealed class ListCourtesyCoursesInputValidator : AbstractValidator<ListCourtesyCoursesInput>
{
    public ListCourtesyCoursesInputValidator()
    {
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 50);
        RuleFor(input => input).Must(input => (long)(input.Page - 1) * input.Size <= int.MaxValue);
        RuleFor(input => input.Title).MinimumLength(1).MaximumLength(100).When(input => input.Title is not null);
    }
}
