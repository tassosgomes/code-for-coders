using FluentValidation;

namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed class CourseWriteContextValidator : AbstractValidator<CourseWriteContext>
{
    public CourseWriteContextValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.ActorId).NotEmpty();
        RuleFor(input => input.ActorName).NotEmpty().MaximumLength(200);
        RuleFor(input => input.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
