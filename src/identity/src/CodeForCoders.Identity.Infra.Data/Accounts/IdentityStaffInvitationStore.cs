using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Identity.Infra.Data.Accounts;

public sealed class IdentityStaffInvitationStore(IdentityDbContext dbContext) : IIdentityStaffInvitationStore
{
    public Task<StaffInvitation?> FindByIdAsync(
        Guid tenantId,
        Guid invitationId,
        CancellationToken cancellationToken)
        => dbContext.StaffInvitations.AsNoTracking().SingleOrDefaultAsync(
            invitation => invitation.TenantId == tenantId && invitation.Id == invitationId,
            cancellationToken);

    public Task<StaffInvitation?> FindUnresolvedByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken)
        => dbContext.StaffInvitations.SingleOrDefaultAsync(
            invitation => invitation.TenantId == tenantId
                && invitation.NormalizedEmail == normalizedEmail
                && invitation.AcceptedOn == null
                && invitation.SupersededOn == null,
            cancellationToken);

    public Task<int> CountPendingAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
        => dbContext.StaffInvitations.CountAsync(
            invitation => invitation.TenantId == tenantId
                && invitation.AcceptedOn == null
                && invitation.SupersededOn == null
                && invitation.ExpiresOn > now,
            cancellationToken);

    public async Task<IReadOnlyList<StaffInvitation>> ListPendingAsync(
        Guid tenantId,
        DateTimeOffset now,
        int page,
        int size,
        CancellationToken cancellationToken)
        => await dbContext.StaffInvitations.AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId
                && invitation.AcceptedOn == null
                && invitation.SupersededOn == null
                && invitation.ExpiresOn > now)
            .OrderByDescending(invitation => invitation.InvitedOn)
            .ThenByDescending(invitation => invitation.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

    public void Add(StaffInvitation invitation) => dbContext.StaffInvitations.Add(invitation);
}
