using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CatalogCourses.ListCatalogCourses;

public sealed class ListCatalogCoursesInputValidator : AbstractValidator<ListCatalogCoursesInput>
{
    public ListCatalogCoursesInputValidator()
    {
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 50);
        RuleFor(input => input).Must(input => (long)(input.Page - 1) * input.Size <= int.MaxValue);
    }
}
