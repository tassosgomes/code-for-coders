namespace CodeForCoders.Identity.Application.Interfaces;

public sealed record StudentAccountResolution(Guid StudentId, string Name, string Email, string Status);
