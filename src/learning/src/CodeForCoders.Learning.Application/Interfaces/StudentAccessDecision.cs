namespace CodeForCoders.Learning.Application.Interfaces;

public sealed record StudentAccessDecision(string Decision, AccessDecisionValidity? Validity, string? DeniedReason, DateTimeOffset? LastExpiredAt, DateTimeOffset DecidedAt);
