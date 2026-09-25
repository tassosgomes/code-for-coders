using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.RevokeStaffRole;

public interface IRevokeStaffRole : IUseCase<RevokeStaffRoleInput, StaffRoleActionOutput>;
