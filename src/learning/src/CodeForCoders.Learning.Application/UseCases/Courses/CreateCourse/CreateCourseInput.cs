namespace CodeForCoders.Learning.Application.UseCases.Courses.CreateCourse;

public sealed record CreateCourseInput(Guid TenantId, Guid ActorId, string ActorName, string IdempotencyKey, string Title, string? Description);
