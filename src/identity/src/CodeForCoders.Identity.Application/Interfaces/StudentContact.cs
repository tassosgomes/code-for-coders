namespace CodeForCoders.Identity.Application.Interfaces;

public sealed record StudentContact(Guid StudentId, string Email, string Name, string Status);
