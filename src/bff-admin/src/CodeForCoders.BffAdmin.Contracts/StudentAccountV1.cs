namespace CodeForCoders.BffAdmin.Contracts;

public sealed record StudentAccountV1(Guid StudentId, string Email, string Name, bool EmailConfirmed, string Status);
