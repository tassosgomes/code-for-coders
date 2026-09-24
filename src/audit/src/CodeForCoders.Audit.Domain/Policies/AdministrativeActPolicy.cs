namespace CodeForCoders.Audit.Domain.Policies;

public static class AdministrativeActPolicy
{
    private static readonly IReadOnlyDictionary<string, bool> AcceptedTypes =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["convite-interno-emitido"] = true,
            ["convite-interno-aceito"] = false,
            ["papel-concedido"] = true,
            ["papel-revogado"] = true,
        };

    public static bool IsAcceptedType(string? type)
        => type is not null && AcceptedTypes.ContainsKey(type);

    public static bool RequiresReason(string? type)
        => type is not null && AcceptedTypes.TryGetValue(type, out var required) && required;
}
