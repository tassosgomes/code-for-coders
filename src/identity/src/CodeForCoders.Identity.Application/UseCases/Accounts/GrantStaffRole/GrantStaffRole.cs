using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;

public sealed class GrantStaffRole(StaffRoleActionExecutor executor) : IGrantStaffRole
{
    public Task<StaffRoleActionOutput> ExecuteAsync(
        GrantStaffRoleInput input,
        CancellationToken cancellationToken)
        => executor.GrantAsync(
            new StaffRoleActionCommand(
                input.TenantId,
                input.ActorAccountId,
                input.TargetAccountId,
                input.Role,
                input.Reason,
                input.IdempotencyKey),
            cancellationToken);
}
