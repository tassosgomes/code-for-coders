using CodeForCoders.Identity.Application.Interfaces;
namespace CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccountEligibility;

public sealed class ConfirmStudentAccountEligibility(IStudentAccountQueries queries) : IConfirmStudentAccountEligibility
{
    public Task<bool> ExecuteAsync(ConfirmStudentAccountEligibilityInput input, CancellationToken cancellationToken)
        => queries.IsEligibleAsync(input.TenantId, input.StudentId, cancellationToken);
}
