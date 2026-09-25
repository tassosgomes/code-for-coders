using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStaffSessionIdentityClient
{
    Task<StaffSessionIdentityCreatedResult> CreateSessionAsync(
        StaffSessionLoginV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffSessionIdentityValidatedResult> ValidateSessionAsync(
        Guid sessionId,
        string? audience,
        CancellationToken cancellationToken);

    Task<StaffSessionIdentityRevokedResult> RevokeSessionAsync(
        Guid sessionId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
