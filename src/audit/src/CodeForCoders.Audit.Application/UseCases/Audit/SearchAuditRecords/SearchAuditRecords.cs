using System.Security.Cryptography;
using CodeForCoders.Audit.Application.Exceptions;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;
using FluentValidation;

namespace CodeForCoders.Audit.Application.UseCases.Audit.SearchAuditRecords;

public sealed class SearchAuditRecords(
    IAuditRecordSearchQueries queries,
    IAuditRecordSnapshotStore snapshotStore,
    IValidator<SearchAuditRecordsInput> validator) : ISearchAuditRecords
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(30);

    public async Task<SearchAuditRecordsOutput> ExecuteAsync(
        SearchAuditRecordsInput input,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var filters = NormalizeFilters(input);
        if (filters.From > filters.To && filters.From is not null && filters.To is not null)
        {
            throw new AuditFilterInvalidException();
        }

        AuditRecordSnapshot snapshot;
        var snapshotId = input.Snapshot;
        if (string.IsNullOrWhiteSpace(snapshotId))
        {
            if (input.Page != 1)
            {
                throw new AuditFilterInvalidException();
            }

            var ids = await queries.SelectOriginalIdsAsync(input.TenantId, filters, cancellationToken);
            snapshot = new AuditRecordSnapshot(input.TenantId, input.SessionId, filters, input.Size, ids);
            snapshotId = CreateSnapshotId();
            if (!await snapshotStore.TryCreateAsync(snapshotId, snapshot, SnapshotLifetime, cancellationToken))
            {
                throw new AuditSnapshotUnavailableException();
            }
        }
        else
        {
            snapshot = await snapshotStore.FindAsync(snapshotId, cancellationToken)
                ?? throw new AuditFilterInvalidException();
            if (snapshot.TenantId != input.TenantId
                || snapshot.SessionId != input.SessionId
                || snapshot.Size != input.Size
                || snapshot.Filters != filters)
            {
                throw new AuditFilterInvalidException();
            }
        }

        var offset = (long)(input.Page - 1) * input.Size;
        var pageIds = offset >= snapshot.RecordIds.Count
            ? []
            : snapshot.RecordIds.Skip((int)offset).Take(input.Size).ToArray();
        var records = pageIds.Length == 0
            ? []
            : await queries.FindOriginalsByIdsAsync(input.TenantId, pageIds, cancellationToken);
        var originalIdsWithComplements = pageIds.Length == 0
            ? []
            : await queries.FindOriginalIdsWithComplementsAsync(input.TenantId, pageIds, cancellationToken);
        var originalsWithComplements = originalIdsWithComplements.ToHashSet();
        var recordsById = records.ToDictionary(record => record.Id);
        var page = pageIds
            .Where(recordsById.ContainsKey)
            .Select(id => ToSummary(recordsById[id], originalsWithComplements.Contains(id)))
            .ToArray();
        var totalPages = snapshot.RecordIds.Count == 0
            ? 0
            : (int)Math.Ceiling(snapshot.RecordIds.Count / (double)input.Size);

        return new SearchAuditRecordsOutput(
            page,
            new AuditRecordPaginationOutput(
                input.Page,
                input.Size,
                snapshot.RecordIds.Count,
                totalPages,
                snapshotId));
    }

    private static AuditRecordSearchFilters NormalizeFilters(SearchAuditRecordsInput input)
        => new(
            input.From?.ToUniversalTime(),
            input.To?.ToUniversalTime(),
            string.IsNullOrWhiteSpace(input.Type) ? null : input.Type.Trim(),
            input.AuthorId,
            input.TargetId,
            input.Compliant);

    private static string CreateSnapshotId()
        => "snap_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static AuditRecordSummaryOutput ToSummary(AuditRecord record, bool hasComplements)
        => new(
            record.Id,
            record.Type,
            record.PracticedOn,
            ToReference(record.AuthorType, record.AuthorId),
            ToReference(record.TargetType, record.TargetId),
            record.Conformity == AuditRecord.Conforming,
            hasComplements);

    private static AuditRecordIdentityReferenceOutput? ToReference(string? type, Guid? id)
        => type is null || id is null
            ? null
            : new AuditRecordIdentityReferenceOutput(type, id.Value);
}
