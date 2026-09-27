namespace CodeForCoders.BffAdmin.Contracts;

public sealed record AuditComplementConfirmationRequestV1(string? Explanation);

public sealed record AuditComplementConfirmationAcceptedV1(Guid ConfirmationId, string Status);

public sealed record ComplementoConfirmadoV1(
    Guid ConfirmationId,
    Guid TenantId,
    Guid OriginalRecordId,
    DateTimeOffset ConfirmedAt,
    ComplementAuthorV1 Author,
    string Explanation);

public sealed record ComplementAuthorV1(string Type, Guid Id);
