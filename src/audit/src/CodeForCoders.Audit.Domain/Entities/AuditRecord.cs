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
    public const int TypeMaxLength = 200;
    public const int ReferenceTypeMaxLength = 100;
    public const int ReasonMaxLength = 1000;
    public const int ComplementValueMaxLength = 100;
    public const int FingerprintLength = 64;
    public const string Conforming = "conforming";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private AuditRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Origin { get; private set; } = string.Empty;

    public Guid FactId { get; private set; }

    public string Type { get; private set; } = string.Empty;

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

    public static AuditRecord CreateConforming(AdministrativeAct act, DateTimeOffset receivedOn)
    {
        ValidateConformingAct(act);

        return new AuditRecord
        {
            Id = Guid.CreateVersion7(receivedOn),
            TenantId = act.TenantId,
            Origin = act.Origin,
            FactId = act.FactId,
            Type = act.Type!,
            AuthorType = act.Author!.Type,
            AuthorId = act.Author.Id,
            TargetType = act.Target!.Type,
            TargetId = act.Target.Id,
            Complement = SerializeComplement(act.Complement),
            Reason = act.Reason,
            PracticedOn = act.PracticedOn!.Value.ToUniversalTime(),
            ReceivedOn = receivedOn.ToUniversalTime(),
            Conformity = Conforming,
            Reasons = [],
            Fingerprint = CalculateFingerprint(act),
        };
    }

    private static void ValidateConformingAct(AdministrativeAct act)
    {
        if (act.FactId == Guid.Empty || act.TenantId == Guid.Empty
            || string.IsNullOrWhiteSpace(act.Origin) || act.Origin.Length > OriginMaxLength
            || string.IsNullOrWhiteSpace(act.Type) || act.Type.Length > TypeMaxLength
            || !AdministrativeActPolicy.IsAcceptedType(act.Type)
            || act.PracticedOn is null
            || act.Author is null || string.IsNullOrWhiteSpace(act.Author.Type)
            || act.Author.Type.Length > ReferenceTypeMaxLength || act.Author.Id == Guid.Empty
            || act.Target is null || string.IsNullOrWhiteSpace(act.Target.Type)
            || act.Target.Type.Length > ReferenceTypeMaxLength || act.Target.Id == Guid.Empty
            || AdministrativeActPolicy.RequiresReason(act.Type) && string.IsNullOrWhiteSpace(act.Reason)
            || act.Reason?.Length > ReasonMaxLength
            || act.Complement?.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key)
                || string.IsNullOrWhiteSpace(pair.Value)
                || pair.Value.Length > ComplementValueMaxLength) is true)
        {
            throw new EntityValidationException("Administrative act is not conforming.");
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
        var canonicalContent = new
        {
            act.TenantId,
            act.Type,
            PracticedOn = act.PracticedOn?.ToUniversalTime(),
            act.Author,
            act.Target,
            Complement = act.Complement is null
                ? null
                : SortComplement(act.Complement),
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
