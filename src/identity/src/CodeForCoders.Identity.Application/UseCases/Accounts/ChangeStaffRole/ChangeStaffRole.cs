using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases.Accounts.StaffRoleActions;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStaffRole;

public sealed class ChangeStaffRole(StaffRoleActionExecutor executor) : IChangeStaffRole
{
    public Task<StaffRoleActionOutput> ExecuteAsync(
        ChangeStaffRoleInput input,
        CancellationToken cancellationToken)
        => executor.ChangeAsync(
            new StaffRoleChangeCommand(
                input.TenantId,
                input.ActorAccountId,
                input.TargetAccountId,
                input.FromRole,
                input.ToRole,
                input.Reason,
                input.IdempotencyKey),
            cancellationToken);
}
