namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record CourseCreationScope(Guid TenantId, Guid ActorId, string Key);
