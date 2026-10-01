using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Showcase.ListShowcaseCourses;

public sealed class ListShowcaseCoursesInputValidator : AbstractValidator<ListShowcaseCoursesInput>
{
    public const int MaxSize = 48;

    private static readonly string[] Levels = ["beginner", "intermediate", "advanced"];

    public ListShowcaseCoursesInputValidator()
    {
        RuleFor(input => input.Level).Must(level => level is null || Levels.Contains(level, StringComparer.Ordinal))
            .WithMessage("level must be beginner, intermediate or advanced.");
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, MaxSize);
        RuleFor(input => input).Must(input => (long)(input.Page - 1) * input.Size <= int.MaxValue)
            .WithName("_page");
    }
}
