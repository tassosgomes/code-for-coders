namespace CodeForCoders.Identity.Application.UseCases.Accounts.ConfirmStudentAccountEligibility;

public sealed record ConfirmStudentAccountEligibilityInput(Guid TenantId, Guid StudentId);
