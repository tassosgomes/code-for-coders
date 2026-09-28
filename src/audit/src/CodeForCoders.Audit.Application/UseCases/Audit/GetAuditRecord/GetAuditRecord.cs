using System.Text.Json;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;

namespace CodeForCoders.Audit.Application.UseCases.Audit.GetAuditRecord;

public sealed class GetAuditRecord(IAuditRecordDetailQueries queries) : IGetAuditRecord
{
    public async Task<AuditRecordDetailOutput?> ExecuteAsync(
        GetAuditRecordInput input,
        CancellationToken cancellationToken)
    {
        var detail = await queries.FindOriginalWithComplementsAsync(input.TenantId, input.RecordId, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        return new AuditRecordDetailOutput(
            detail.Original.Id,
            detail.Original.Type,
            detail.Original.PracticedOn,
            ToReference(detail.Original.AuthorType, detail.Original.AuthorId),
            ToReference(detail.Original.TargetType, detail.Original.TargetId),
            detail.Original.Conformity == AuditRecord.Conforming,
            detail.Complements.Count > 0,
            detail.Original.Origin,
            detail.Original.ReceivedOn,
            detail.Original.Reason,
            DeserializeAttributes(detail.Original.Complement),
            detail.Original.Reasons,
            detail.Complements.Select(ToComplement).ToArray());
    }

    private static IReadOnlyDictionary<string, string> DeserializeAttributes(string? json)
        => json is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private static AuditRecordDetailIdentityReferenceOutput? ToReference(string? type, Guid? id)
        => type is null || id is null
            ? null
            : new AuditRecordDetailIdentityReferenceOutput(type, id.Value);

    private static AuditRecordComplementOutput ToComplement(AuditRecord complement)
        => new(
            complement.Id,
            complement.ConfirmationId!.Value,
            complement.ConfirmedAt!.Value,
            ToReference(complement.AuthorType, complement.AuthorId),
            complement.Explanation!);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
