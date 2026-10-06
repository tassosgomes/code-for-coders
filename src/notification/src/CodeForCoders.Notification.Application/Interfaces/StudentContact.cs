namespace CodeForCoders.Notification.Application.Interfaces;

public sealed record StudentContact(Guid StudentId, string Email, string Name, string Status);
