namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentCourseAccess(Guid CourseId, string Status, DateTimeOffset? Since,
    DateOnly? EndedOn, DateTimeOffset? EndedAt, string? EndedReason);
