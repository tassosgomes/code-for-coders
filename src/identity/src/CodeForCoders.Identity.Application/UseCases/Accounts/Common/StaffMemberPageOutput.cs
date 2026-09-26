namespace CodeForCoders.Identity.Application.UseCases.Accounts.Common;

public sealed record StaffMemberPageOutput(
    IReadOnlyList<StaffMemberOutput> Data,
    StaffMemberPaginationOutput Pagination);
