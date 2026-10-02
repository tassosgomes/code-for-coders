namespace CodeForCoders.Commerce.Application.Interfaces;

public sealed record StudentAccessGrant(Guid GrantId, Guid StudentId, Guid CourseId, string CourseTitle, string Origin,
    string Status, StudentAccessPeriod AccessPeriod, DateTimeOffset GrantedAt, DateOnly? EndsOn, DateTimeOffset? ExpiresAt, string? Reason);
