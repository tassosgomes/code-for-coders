namespace CodeForCoders.Media.Application.Interfaces;

public sealed record AccessDecisionQuery(Guid TenantId, Guid StudentId, Guid CourseId);
