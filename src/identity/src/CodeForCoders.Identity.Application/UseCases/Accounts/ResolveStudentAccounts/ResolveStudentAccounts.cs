using CodeForCoders.Identity.Application.Exceptions;
using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.ValidateStaffSession;
using FluentValidation;
using CodeForCoders.Identity.Domain.Entities;
namespace CodeForCoders.Identity.Application.UseCases.Accounts.ResolveStudentAccounts;

public sealed class ResolveStudentAccounts(IStudentAccountQueries queries, IValidateStaffSession sessionValidator,
    IValidator<ResolveStudentAccountsInput> validator) : IResolveStudentAccounts
{
    public async Task<StudentAccountResolutionList> ExecuteAsync(ResolveStudentAccountsInput input, CancellationToken cancellationToken)
    {
        var session = await sessionValidator.ExecuteAsync(new(input.TenantId, input.SessionId), cancellationToken);
        if (session is null) throw new StudentAccountLookupException(401, "SESSION_REQUIRED", "A current staff session is required.");
        if (!session.Session.Permissions.Contains(StaffRoleCatalog.ReadFinance, StringComparer.Ordinal))
            throw new StudentAccountLookupException(403, "PERMISSION_DENIED", "The current session cannot resolve student accounts.");
        if (!(await validator.ValidateAsync(input, cancellationToken)).IsValid)
            throw new StudentAccountLookupException(400, "VALIDATION_ERROR", "One to fifty unique student identifiers are required.");
        return new(await queries.ResolveAsync(input.TenantId, input.StudentIds!, cancellationToken));
    }
}
