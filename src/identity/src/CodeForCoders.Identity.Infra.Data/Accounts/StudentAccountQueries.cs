using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class StudentAccountQueries(IdentityDbContext dbContext) : IStudentAccountQueries
{
    public Task<StudentContact?> FindContactAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken)
        => dbContext.Accounts.AsNoTracking().IgnoreQueryFilters()
            .Where(account => account.TenantId == tenantId && account.Id == studentId && account.Type == AccountType.Student)
            .Select(account => new StudentContact(account.Id, account.Email, account.Name,
                account.DeactivatedOn == null ? "active" : "disabled"))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsEligibleAsync(Guid tenantId, Guid studentId, CancellationToken cancellationToken)
        => dbContext.Accounts.AsNoTracking().IgnoreQueryFilters().AnyAsync(account => account.TenantId == tenantId
            && account.Id == studentId && account.Type == AccountType.Student && account.DeactivatedOn == null, cancellationToken);

    public Task<StudentAccountDetails?> FindAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken)
        => dbContext.Accounts.AsNoTracking().IgnoreQueryFilters()
            .Where(account => account.TenantId == tenantId && account.Type == AccountType.Student
                && account.NormalizedEmail == normalizedEmail)
            .OrderBy(account => account.DeactivatedOn != null)
            .ThenByDescending(account => account.DeactivatedOn)
            .ThenByDescending(account => account.Id)
            .Select(account => new StudentAccountDetails(account.Id, account.NormalizedEmail, account.Name,
                account.IsConfirmed, account.DeactivatedOn == null ? "active" : "disabled"))
            .FirstOrDefaultAsync(cancellationToken);
}
