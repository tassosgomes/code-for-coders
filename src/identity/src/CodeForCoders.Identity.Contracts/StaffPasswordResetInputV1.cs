namespace CodeForCoders.Identity.Contracts;

public sealed record StaffPasswordResetInputV1(string Token, string NewPassword);
