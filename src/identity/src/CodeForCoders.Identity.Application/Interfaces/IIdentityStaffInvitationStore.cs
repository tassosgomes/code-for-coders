using CodeForCoders.Identity.Domain.Entities;

namespace CodeForCoders.Identity.Application.Interfaces;

public interface IIdentityStaffInvitationStore
{
    Task<StaffInvitation?> FindByIdAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken);

    Task<StaffInvitation?> FindByTokenHashAsync(Guid tenantId, string tokenHash, CancellationToken cancellationToken);

    Task<StaffInvitation?> FindUnresolvedByNormalizedEmailAsync(
        Guid tenantId,
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<int> CountPendingAsync(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffInvitation>> ListPendingAsync(
        Guid tenantId,
        DateTimeOffset now,
        int page,
        int size,
        CancellationToken cancellationToken);

    void Add(StaffInvitation invitation);
}
