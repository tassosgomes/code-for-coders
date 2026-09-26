using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using CodeForCoders.Identity.Application.UseCases;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;

public interface IListStaffMembers : IUseCase<ListStaffMembersInput, StaffMemberPageOutput>;
