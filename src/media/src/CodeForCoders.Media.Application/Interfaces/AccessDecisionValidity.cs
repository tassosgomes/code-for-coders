namespace CodeForCoders.Media.Application.Interfaces;

public sealed record AccessDecisionValidity(string Type, DateTimeOffset? ExpiresAt);
