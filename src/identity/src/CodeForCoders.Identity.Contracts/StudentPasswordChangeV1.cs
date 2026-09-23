namespace CodeForCoders.Identity.Contracts;

public sealed record StudentPasswordChangeV1(
    Guid SessionId,
    string? CurrentPassword,
    string? NewPassword);
