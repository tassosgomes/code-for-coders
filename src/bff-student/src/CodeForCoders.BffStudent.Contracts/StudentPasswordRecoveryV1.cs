namespace CodeForCoders.BffStudent.Contracts;

public sealed record StudentPasswordResetRequestV1(string? Email);

public sealed record StudentPasswordResetV1(string? Token, string? NewPassword);
