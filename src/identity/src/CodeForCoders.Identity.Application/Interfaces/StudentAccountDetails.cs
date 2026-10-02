namespace CodeForCoders.Identity.Application.Interfaces;

public sealed record StudentAccountDetails(Guid StudentId, string Email, string Name, bool EmailConfirmed, string Status);
