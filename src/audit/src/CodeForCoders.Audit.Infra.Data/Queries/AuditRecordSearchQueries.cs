using System.Data;
using CodeForCoders.Audit.Application.Interfaces;
using CodeForCoders.Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Audit.Infra.Data.Queries;

public sealed class AuditRecordSearchQueries(AuditDbContext dbContext) : IAuditRecordSearchQueries
{
    public async Task<IReadOnlyList<Guid>> SelectOriginalIdsAsync(
        Guid tenantId,
        AuditRecordSearchFilters filters,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var query = ApplyFilters(dbContext.AuditRecords.AsNoTracking(), tenantId, filters);
        var ids = await query
            .OrderByDescending(record => record.PracticedOn ?? record.ReceivedOn)
            .ThenByDescending(record => record.Id)
            .Select(record => record.Id)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ids;
    }

    public async Task<IReadOnlyList<AuditRecord>> FindOriginalsByIdsAsync(
        Guid tenantId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
        => await dbContext.AuditRecords.AsNoTracking()
            .Where(record => record.TenantId == tenantId && ids.Contains(record.Id))
            .ToListAsync(cancellationToken);

    private static IQueryable<AuditRecord> ApplyFilters(
        IQueryable<AuditRecord> query,
        Guid tenantId,
        AuditRecordSearchFilters filters)
    {
        query = query.Where(record => record.TenantId == tenantId);
        if (filters.From is not null)
        {
            query = query.Where(record => record.PracticedOn != null && record.PracticedOn >= filters.From.Value);
        }

        if (filters.To is not null)
        {
            query = query.Where(record => record.PracticedOn != null && record.PracticedOn <= filters.To.Value);
        }

        if (filters.Type is not null)
        {
            query = query.Where(record => record.Type == filters.Type);
        }

        if (filters.AuthorId is not null)
        {
            query = query.Where(record => record.AuthorId == filters.AuthorId);
        }

        if (filters.TargetId is not null)
        {
            query = query.Where(record => record.TargetId == filters.TargetId);
        }

        if (filters.Compliant is not null)
        {
            var conformity = filters.Compliant.Value ? AuditRecord.Conforming : AuditRecord.NonConforming;
            query = query.Where(record => record.Conformity == conformity);
        }

        return query;
    }
}
