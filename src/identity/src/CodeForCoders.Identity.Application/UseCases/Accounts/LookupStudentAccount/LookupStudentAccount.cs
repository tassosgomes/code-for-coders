using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using CodeForCoders.Identity.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.LookupStudentAccount;

public sealed class LookupStudentAccount(
    IStudentAccountQueries queries,
    IValidateStaffSession sessionValidator,
    IValidator<LookupStudentAccountInput> validator) : ILookupStudentAccount
{
    public async Task<StudentAccountDetails> ExecuteAsync(LookupStudentAccountInput input, CancellationToken cancellationToken)
    {
        var session = await sessionValidator.ExecuteAsync(new ValidateStaffSessionInput(input.TenantId, input.SessionId), cancellationToken);
        if (session is null)
        {
            throw new StudentAccountLookupException(401, "SESSION_REQUIRED", "A current staff session is required.");
        }

        if (!session.Session.Permissions.Contains(StaffRoleCatalog.GrantCourtesy, StringComparer.Ordinal))
        {
            throw new StudentAccountLookupException(403, "PERMISSION_DENIED", "The current session cannot look up student accounts.");
        }

        var normalized = input with { Email = input.Email?.Trim().ToLowerInvariant() };
        if (!(await validator.ValidateAsync(normalized, cancellationToken)).IsValid)
        {
            throw new StudentAccountLookupException(400, "VALIDATION_ERROR", "A valid student email is required.");
        }

        return await queries.FindAsync(input.TenantId, normalized.Email!, cancellationToken)
            ?? throw new StudentAccountLookupException(404, "STUDENT_ACCOUNT_NOT_FOUND", "Student account not found.");
    }
}
