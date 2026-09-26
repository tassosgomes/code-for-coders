namespace CodeForCoders.BffAdmin.Contracts;

public sealed record StaffPasswordResetRequestV1(string Token, string NewPassword);
