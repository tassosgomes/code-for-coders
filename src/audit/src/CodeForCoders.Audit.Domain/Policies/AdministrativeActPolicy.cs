using CodeForCoders.Audit.Domain.Entities;
using CodeForCoders.Audit.Domain.ValueObjects;
using System.Globalization;

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
            ["cortesia-concedida"] = true,
            ["versao-publicada"] = false,
            ["oferta-publicada"] = false,
            ["oferta-alterada"] = false,
            ["oferta-despublicada"] = false,
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
        var isCourtesyType = act.Type == "cortesia-concedida";
        var isOfferType = act.Type is "oferta-publicada" or "oferta-alterada" or "oferta-despublicada";
        if (!IsAcceptedType(act.Type) || (isOfferType && act.Origin != "catalogo") || (act.Type == "versao-publicada" && act.Origin != "conteudo")
            || (isCourtesyType && act.Origin != "matricula") || (act.Origin == "matricula" && !isCourtesyType))
        {
            reasons.Add(UnknownType);
        }

        if (!IsValidReference(act.Author))
        {
            reasons.Add(MissingAuthor);
        }

        if (!IsValidReference(act.Target) || (act.Type == "versao-publicada" && act.Target?.Type != "curso")
            || (act.Origin == "catalogo" && act.Target?.Type != "oferta")
            || (isCourtesyType && act.Target?.Type != "conta-aluno"))
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

        if (act.ComplementIsInvalid || !IsValidComplement(act.Complement)
            || act.Type == "oferta-alterada" && !HasValidChangePairs(act.Complement)
            || isCourtesyType && !HasCourtesyComplement(act.Complement))
        {
            reasons.Add(InvalidComplement);
        }

        if (act.Reason?.Length > AuditRecord.ReasonMaxLength)
        {
            reasons.Add(ReasonExceedsLimit);
        }

        return reasons.ToArray();
    }

    private static bool HasCourtesyComplement(IReadOnlyDictionary<string, string>? complement)
        => complement is { Count: 3 } && complement.TryGetValue("curso", out var course) && Guid.TryParse(course, out var courseId)
            && courseId != Guid.Empty && complement.TryGetValue("concessao", out var grant) && Guid.TryParse(grant, out var grantId)
            && grantId != Guid.Empty && complement.TryGetValue("vigencia", out var period) && IsValidPeriod(period);

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

    private static bool HasValidChangePairs(IReadOnlyDictionary<string, string>? complement)
        => HasValidPair(complement, "precoAnterior", "precoNovo", IsValidPrice)
            && HasValidPair(complement, "vigenciaAnterior", "vigenciaNova", IsValidPeriod);

    private static bool HasValidPair(IReadOnlyDictionary<string, string>? complement, string before, string after,
        Func<string, bool> validate)
    {
        if (complement is null) return true;
        var hasBefore = complement.TryGetValue(before, out var previous);
        var hasAfter = complement.TryGetValue(after, out var current);
        return hasBefore == hasAfter && (!hasBefore || previous is not null && current is not null
            && validate(previous) && validate(current));
    }

    private static bool IsValidPrice(string value)
        => value.Length > 0 && value[0] is >= '1' and <= '9' && value.All(character => character is >= '0' and <= '9');

    private static bool IsValidPeriod(string value)
        => value == "vitalicia" || value.Length is >= 2 and <= 3 && value[^1] == 'm' && value[0] is >= '1' and <= '9'
            && int.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var months)
            && months is >= 1 and <= 60;

    private static bool IsValidComplement(IReadOnlyDictionary<string, string>? complement)
        => complement is null || complement.All(pair =>
            !string.IsNullOrWhiteSpace(pair.Key)
            && pair.Value is not null
            && pair.Value.Length <= AuditRecord.ComplementValueMaxLength);
}
