using FluentValidation;

namespace CodeForCoders.Commerce.Application.UseCases.CourtesyGrants.GrantCourtesy;

public sealed class GrantCourtesyInputValidator : AbstractValidator<GrantCourtesyInput>
{
    public GrantCourtesyInputValidator()
    {
        RuleFor(item => item.StudentId).NotEmpty();
        RuleFor(item => item.CourseId).NotEmpty();
        RuleFor(item => item.ActorId).NotEmpty();
        RuleFor(item => item.Reason).Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 500);
        RuleFor(item => item.AccessPeriod).Must(period => period is not null &&
            (period.Type == "lifetime" && period.Months is null && !period.MonthsSpecified || period.Type == "months" && period.Months is >= 1 and <= 60));
    }
}
