using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeForCoders.Audit.Domain.Policies;
using CodeForCoders.Audit.Domain.SeedWork;
using CodeForCoders.Audit.Domain.ValueObjects;

namespace CodeForCoders.Audit.Domain.Entities;

/// <summary>
/// Append-only evidence of an administrative act received from a domain.
/// </summary>
public sealed class AuditRecord
{
    public const int OriginMaxLength = 100;
    public const int ReasonMaxLength = 1000;
    public const int ComplementValueMaxLength = 100;
    public const int FingerprintLength = 64;
    public const string Conforming = "conforming";
    public const string NonConforming = "non_conforming";
    public const string OriginalRecordType = "original";
    public const string ComplementRecordType = "complement";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private AuditRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string RecordType { get; private set; } = OriginalRecordType;

    public string Origin { get; private set; } = string.Empty;

    public Guid FactId { get; private set; }

    public Guid? OriginalRecordId { get; private set; }

    public Guid? ConfirmationId { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public string? Explanation { get; private set; }

    public string? Type { get; private set; }

    public string? AuthorType { get; private set; }

    public Guid? AuthorId { get; private set; }

    public string? TargetType { get; private set; }

    public Guid? TargetId { get; private set; }

    public string? Complement { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset? PracticedOn { get; private set; }

    public DateTimeOffset ReceivedOn { get; private set; }

    public string Conformity { get; private set; } = string.Empty;

    public string[] Reasons { get; private set; } = [];

    public string Fingerprint { get; private set; } = string.Empty;

    public static AuditRecord Create(AdministrativeAct act, DateTimeOffset receivedOn)
    {
        ValidateReadableEnvelope(act);
        var reasons = AdministrativeActPolicy.GetNonConformityReasons(act);

        return new AuditRecord
        {
            Id = Guid.CreateVersion7(receivedOn),
            TenantId = act.TenantId,
            RecordType = OriginalRecordType,
            Origin = act.Origin,
            FactId = act.FactId,
            Type = act.Type,
            AuthorType = act.Author?.Type,
            AuthorId = act.Author?.Id,
            TargetType = act.Target?.Type,
            TargetId = act.Target?.Id,
            Complement = reasons.Contains("complemento-invalido", StringComparer.Ordinal)
                ? null
                : SerializeComplement(act.Complement),
            Reason = act.Reason,
            PracticedOn = act.PracticedOn?.ToUniversalTime(),
            ReceivedOn = receivedOn.ToUniversalTime(),
            Conformity = reasons.Length is 0 ? Conforming : NonConforming,
            Reasons = reasons,
            Fingerprint = CalculateFingerprint(act),
        };
    }

    public static AuditRecord CreateComplement(
        Guid tenantId,
        Guid originalRecordId,
        Guid confirmationId,
        DateTimeOffset confirmedAt,
        string authorType,
        Guid authorId,
        string explanation)
    {
        if (tenantId == Guid.Empty || originalRecordId == Guid.Empty || confirmationId == Guid.Empty
            || authorId == Guid.Empty || string.IsNullOrWhiteSpace(authorType)
            || string.IsNullOrWhiteSpace(explanation) || explanation.Length > ReasonMaxLength)
        {
            throw new EntityValidationException("Audit record complement is invalid.");
        }

        var timestamp = confirmedAt.ToUniversalTime();
        return new AuditRecord
        {
            Id = Guid.CreateVersion7(timestamp),
            TenantId = tenantId,
            RecordType = ComplementRecordType,
            Origin = "audit-complement",
            FactId = confirmationId,
            OriginalRecordId = originalRecordId,
            ConfirmationId = confirmationId,
            ConfirmedAt = timestamp,
            ReceivedOn = timestamp,
            AuthorType = authorType,
            AuthorId = authorId,
            Conformity = Conforming,
            Reasons = [],
            Fingerprint = string.Empty,
            Explanation = explanation,
        };
    }

    private static void ValidateReadableEnvelope(AdministrativeAct act)
    {
        if (act.FactId == Guid.Empty || act.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(act.Origin) || act.Origin.Length > OriginMaxLength)
        {
            throw new EntityValidationException("Administrative act envelope is invalid.");
        }
    }

    private static string? SerializeComplement(IReadOnlyDictionary<string, string>? complement)
        => complement is null
            ? null
            : JsonSerializer.Serialize(
                SortComplement(complement),
                CanonicalJsonOptions);

    private static string CalculateFingerprint(AdministrativeAct act)
    {
        object? complement = act.Complement is null
            ? act.InvalidComplementFingerprint is null
                ? null
                : "invalid:" + act.InvalidComplementFingerprint
            : SortComplement(act.Complement);
        var canonicalContent = new
        {
            act.TenantId,
            act.Type,
            PracticedOn = act.PracticedOn?.ToUniversalTime(),
            act.Author,
            act.Target,
            Complement = complement,
            act.Reason,
        };
        var canonicalJson = JsonSerializer.Serialize(canonicalContent, CanonicalJsonOptions);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static SortedDictionary<string, string> SortComplement(
        IReadOnlyDictionary<string, string> complement)
        => new(
            complement.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
}
