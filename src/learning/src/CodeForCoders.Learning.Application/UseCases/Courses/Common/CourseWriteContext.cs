namespace CodeForCoders.Learning.Application.UseCases.Courses.Common;

public sealed record CourseWriteContext(Guid CourseId, Guid TenantId, Guid ActorId, string ActorName, string IdempotencyKey, string RequestJson);
