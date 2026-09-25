namespace CodeForCoders.Identity.Application.UseCases.Accounts.Common;

public sealed record StaffMemberPaginationOutput(int Page, int Size, int Total, int TotalPages);
