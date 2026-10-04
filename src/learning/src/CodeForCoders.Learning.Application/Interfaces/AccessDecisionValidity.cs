namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record AccessDecisionValidity(string Type, DateTimeOffset? ExpiresAt);
