namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentCourseAccessQuery(Guid StudentId, DateTimeOffset Now);
