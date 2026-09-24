using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;

namespace CodeForCoders.Audit.Domain.Policies;

public static class AdministrativeActPolicy
{
    private const string UnknownType = "tipo-desconhecido";
    private const string MissingAuthor = "autor-ausente";
    private const string MissingTarget = "alvo-ausente";
    private const string MissingReason = "motivo-ausente";
    private const string MissingPracticedOn = "momento-ausente";
    private const string InvalidComplement = "complemento-invalido";
    private const string ReasonExceedsLimit = "motivo-excede-limite";

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

    public static string[] GetNonConformityReasons(AdministrativeAct act)
    {
        var reasons = new List<string>(capacity: 7);
        if (!IsAcceptedType(act.Type))
        {
            reasons.Add(UnknownType);
        }

        if (!IsValidReference(act.Author))
        {
            reasons.Add(MissingAuthor);
        }

        if (!IsValidReference(act.Target))
        {
            reasons.Add(MissingTarget);
        }

        if (RequiresReason(act.Type) && string.IsNullOrWhiteSpace(act.Reason))
        {
            reasons.Add(MissingReason);
        }

        if (act.PracticedOn is null)
        {
            reasons.Add(MissingPracticedOn);
        }

        if (act.ComplementIsInvalid || !IsValidComplement(act.Complement))
        {
            reasons.Add(InvalidComplement);
        }

        if (act.Reason?.Length > AuditRecord.ReasonMaxLength)
        {
            reasons.Add(ReasonExceedsLimit);
        }

        return reasons.ToArray();
    }

    private static bool IsValidReference(AdministrativeActReference? reference)
        => reference is not null
            && IsValidReferenceType(reference.Type)
            && reference.Id is not null
            && reference.Id != Guid.Empty;

    private static bool IsValidReferenceType(string? type)
    {
        if (string.IsNullOrEmpty(type) || !IsAsciiLowercase(type[0]))
        {
            return false;
        }

        return type.Skip(1).All(character =>
            IsAsciiLowercase(character)
            || character is >= '0' and <= '9'
            || character is '-');
    }

    private static bool IsAsciiLowercase(char value) => value is >= 'a' and <= 'z';

    private static bool IsValidComplement(IReadOnlyDictionary<string, string>? complement)
        => complement is null || complement.All(pair =>
            !string.IsNullOrWhiteSpace(pair.Key)
            && pair.Value is not null
            && pair.Value.Length <= AuditRecord.ComplementValueMaxLength);
}
