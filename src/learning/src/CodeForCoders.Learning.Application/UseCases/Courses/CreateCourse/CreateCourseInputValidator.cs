using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;

public sealed class CreateCourseInputValidator : AbstractValidator<CreateCourseInput>
{
    public CreateCourseInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.ActorName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(input => input.Title).NotNull().MaximumLength(200);
        RuleFor(input => input.Description).MaximumLength(5000);
    }
}
