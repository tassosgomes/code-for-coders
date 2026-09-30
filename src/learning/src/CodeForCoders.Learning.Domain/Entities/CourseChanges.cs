namespace CodeForCoders.Learning.Domain.Entities;

public sealed record CourseChanges(string? Title, string? Description, bool HasDescription, int? Position, Guid? ModuleId);
