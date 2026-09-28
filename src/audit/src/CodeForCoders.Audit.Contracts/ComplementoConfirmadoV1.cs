namespace CodeForCoders.Audit.Contracts;

public sealed record ComplementoConfirmadoV1(
    Guid ConfirmationId,
    Guid TenantId,
    Guid OriginalRecordId,
    DateTimeOffset ConfirmedAt,
    ComplementAuthorV1 Author,
    string Explanation);

public sealed record ComplementAuthorV1(string Type, Guid Id);
