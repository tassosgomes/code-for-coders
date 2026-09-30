namespace CodeForCoders.BffAdmin.Application.Interfaces;

public sealed record CourseClientRequest(string Path, string AccessToken, string? ActorName, string? IdempotencyKey, CourseCreateBody? Body);
