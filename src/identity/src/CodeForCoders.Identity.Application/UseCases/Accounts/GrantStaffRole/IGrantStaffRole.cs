using CodeForCoders.Identity.Application.UseCases;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.GrantStaffRole;

public interface IGrantStaffRole : IUseCase<GrantStaffRoleInput, StaffRoleActionOutput>;
