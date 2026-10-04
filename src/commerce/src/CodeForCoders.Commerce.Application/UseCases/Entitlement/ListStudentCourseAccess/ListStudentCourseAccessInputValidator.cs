using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.ListStudentCourseAccess;

public sealed class ListStudentCourseAccessInputValidator : AbstractValidator<ListStudentCourseAccessInput>
{
    public ListStudentCourseAccessInputValidator() => RuleFor(input => input.StudentId).NotEmpty();
}
