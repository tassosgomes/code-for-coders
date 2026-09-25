using CodeForCoders.Identity.Application.Interfaces;
using CodeForCoders.Identity.Application.UseCases.Accounts.Common;
using FluentValidation;

namespace CodeForCoders.Identity.Application.UseCases.Accounts.ListStaffMembers;

public sealed class ListStaffMembers(
    IIdentityStaffAccountStore staffAccountStore,
    IValidator<ListStaffMembersInput> validator) : IListStaffMembers
{
    public async Task<StaffMemberPageOutput> ExecuteAsync(
        ListStaffMembersInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var total = await staffAccountStore.CountInternalStaffMembersAsync(input.TenantId, cancellationToken);
        var members = await staffAccountStore.ListInternalStaffMembersAsync(
            input.TenantId,
            input.Page,
            input.Size,
            cancellationToken);
        return new StaffMemberPageOutput(
            members.Select(member => new StaffMemberOutput(
                member.AccountId,
                member.Name,
                member.Email,
                member.Roles,
                member.AccountId == input.ActorAccountId)).ToArray(),
            new StaffMemberPaginationOutput(
                input.Page,
                input.Size,
                total,
                total == 0 ? 0 : (int)Math.Ceiling(total / (double)input.Size)));
    }
}
