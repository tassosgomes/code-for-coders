using System.Text.Json.Serialization;

namespace CodeForCoders.Commerce.Application.UseCases.Entitlement.DecideAccess;

public sealed record AccessValidityOutput(string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresAt);
