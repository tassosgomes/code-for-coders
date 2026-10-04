namespace CodeForCoders.Media.Domain.Entities;

public sealed record PlaybackSessionCreateInput(Guid TenantId, Guid StudentId, Guid LessonId,
    Guid CourseId, Guid VideoId, DateTimeOffset Now);
