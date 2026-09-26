using CodeForCoders.BffAdmin.Contracts;

namespace CodeForCoders.BffAdmin.Api.Clients;

public interface IStaffPasswordResetIdentityClient
{
    Task<StaffPasswordRecoveryIdentityResult> RequestPasswordResetAsync(
        StaffPasswordRecoveryRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<StaffPasswordResetIdentityResult> ResetPasswordAsync(
        StaffPasswordResetRequestV1 request,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
