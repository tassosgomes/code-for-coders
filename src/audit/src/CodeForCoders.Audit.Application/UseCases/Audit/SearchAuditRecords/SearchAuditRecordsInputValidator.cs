using FluentValidation;

namespace CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;

public sealed class SearchAuditRecordsInputValidator : AbstractValidator<SearchAuditRecordsInput>
{
    public SearchAuditRecordsInputValidator()
    {
        RuleFor(input => input.TenantId).NotEmpty();
        RuleFor(input => input.SessionId).NotEmpty();
        RuleFor(input => input.Page).GreaterThan(0);
        RuleFor(input => input.Size).InclusiveBetween(1, 50);
        RuleFor(input => input.Type).Must(type => type is null || !string.IsNullOrWhiteSpace(type));
    }
}
