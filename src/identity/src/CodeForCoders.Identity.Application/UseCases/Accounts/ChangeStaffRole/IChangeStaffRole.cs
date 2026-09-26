using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ChangeStaffRole;

public interface IChangeStaffRole : IUseCase<ChangeStaffRoleInput, StaffRoleActionOutput>;
