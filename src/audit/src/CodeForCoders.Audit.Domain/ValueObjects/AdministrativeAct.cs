namespace CodeForCoders.Audit.Domain.ValueObjects;

public sealed record AdministrativeAct(
    Guid FactId,
    string Origin,
    string? Type,
    Guid TenantId,
    DateTimeOffset? PracticedOn,
    AdministrativeActReference? Author,
    AdministrativeActReference? Target,
    IReadOnlyDictionary<string, string>? Complement,
    string? Reason,
    bool ComplementIsInvalid = false,
    string? InvalidComplementFingerprint = null);
