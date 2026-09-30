namespace CodeForCoders.Learning.Domain.Entities;

public sealed record CourseCreation(Guid TenantId, Guid ActorId, string ActorName, string Title, string? Description, DateTimeOffset Now);
