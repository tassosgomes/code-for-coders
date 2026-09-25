using System.Text.Json.Serialization;

namespace CodeForCoders.BffAdmin.Contracts;

public sealed record StaffSessionLoginV1(string? Email, string? Password);

public sealed record StaffSessionReferenceV1(Guid SessionId);

public sealed record StaffSessionValidationV1(Guid SessionId, string? Audience);

public sealed record StaffSessionCreatedV1(
    Guid SessionId,
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt);

public sealed record StaffSessionValidatedV1(
    Guid AccountId,
    string Name,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AccessToken);
