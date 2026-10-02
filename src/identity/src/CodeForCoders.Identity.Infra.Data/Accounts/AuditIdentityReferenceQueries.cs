using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class AuditIdentityReferenceQueries(IdentityDbContext dbContext) : IAuditIdentityReferenceQueries
{
    public async Task<IReadOnlyDictionary<AuditIdentityReferenceKey, string>> ResolveLabelsAsync(
        Guid tenantId,
        IReadOnlyList<AuditIdentityReferenceKey> references,
        CancellationToken cancellationToken)
    {
        var accountIds = references
            .Where(reference => reference.Type == "conta-interna")
            .Select(reference => reference.Id)
            .Distinct()
            .ToArray();
        var invitationIds = references
            .Where(reference => reference.Type == "convite-interno")
            .Select(reference => reference.Id)
            .Distinct()
            .ToArray();
        var studentIds = references
            .Where(reference => reference.Type == "conta-aluno")
            .Select(reference => reference.Id)
            .Distinct()
            .ToArray();

        var labels = new Dictionary<AuditIdentityReferenceKey, string>();
        if (accountIds.Length > 0)
        {
            var accounts = await dbContext.Accounts.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(account => account.TenantId == tenantId
                    && account.Type == AccountType.InternalActor
                    && accountIds.Contains(account.Id))
                .Select(account => new { account.Id, account.Name })
                .ToListAsync(cancellationToken);
            foreach (var account in accounts)
            {
                labels[new AuditIdentityReferenceKey("conta-interna", account.Id)] = account.Name;
            }
        }

        if (studentIds.Length > 0)
        {
            var students = await dbContext.Accounts.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(account => account.TenantId == tenantId
                    && account.Type == AccountType.Student
                    && studentIds.Contains(account.Id))
                .Select(account => new { account.Id, account.Name })
                .ToListAsync(cancellationToken);
            foreach (var student in students)
            {
                labels[new AuditIdentityReferenceKey("conta-aluno", student.Id)] = student.Name;
            }
        }

        if (invitationIds.Length > 0)
        {
            var invitations = await dbContext.StaffInvitations.AsNoTracking()
                .IgnoreQueryFilters()
                .Where(invitation => invitation.TenantId == tenantId && invitationIds.Contains(invitation.Id))
                .Select(invitation => new { invitation.Id, invitation.Email })
                .ToListAsync(cancellationToken);
            foreach (var invitation in invitations)
            {
                labels[new AuditIdentityReferenceKey("convite-interno", invitation.Id)] = invitation.Email;
            }
        }

        return labels;
    }
}
