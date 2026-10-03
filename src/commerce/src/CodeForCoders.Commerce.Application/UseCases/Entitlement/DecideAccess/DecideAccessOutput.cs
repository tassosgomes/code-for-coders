using CodeForCoders.Commerce.Domain.ValueObjects;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;

public sealed record DecideAccessOutput(string Decision, AccessValidityOutput? Validity, string? DeniedReason,
    DateTimeOffset? LastExpiredAt, DateTimeOffset DecidedAt)
{
    public static DecideAccessOutput FromAccessDecision(AccessDecision decision)
        => new(decision.Decision,
            decision.ValidityType is { } type ? new(type, decision.ExpiresAt) : null,
            decision.DeniedReason, decision.LastExpiredAt, decision.DecidedAt);
}
