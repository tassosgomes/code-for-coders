using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;

public sealed class RevokeStaffRole(StaffRoleActionExecutor executor) : IRevokeStaffRole
{
    public Task<StaffRoleActionOutput> ExecuteAsync(
        RevokeStaffRoleInput input,
        CancellationToken cancellationToken)
        => executor.RevokeAsync(
            new StaffRoleActionCommand(
                input.TenantId,
                input.ActorAccountId,
                input.TargetAccountId,
                input.Role,
                input.Reason,
                input.IdempotencyKey),
            cancellationToken);
}
