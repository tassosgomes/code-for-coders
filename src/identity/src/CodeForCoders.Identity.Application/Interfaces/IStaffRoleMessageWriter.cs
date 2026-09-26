namespace CodeForCoders.Identity.Application.Interfaces;

public interface IStaffRoleMessageWriter
{
    Task AppendRoleGrantedAsync(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        DateTimeOffset practicedOn,
        string? correlationId,
        CancellationToken cancellationToken);

    Task AppendRoleRevokedAsync(
        Guid tenantId,
        Guid actorAccountId,
        Guid targetAccountId,
        string role,
        string reason,
        DateTimeOffset practicedOn,
        string? correlationId,
        CancellationToken cancellationToken);
}
