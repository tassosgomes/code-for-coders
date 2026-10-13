using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionEventRepository(MediaDbContext dbContext) : ITranscriptionEventRepository
{
    public async Task<bool> RecordAsync(TranscriptionNotification notification,
        Func<Transcription?, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var attempts = await dbContext.Transcriptions.FromSqlInterpolated($"""
                SELECT * FROM media_access.video_transcriptions
                WHERE transcription_id = {notification.TranscriptionId ?? Guid.Empty} FOR UPDATE
                """).IgnoreQueryFilters().ToArrayAsync(cancellationToken);
            var attempt = attempts.SingleOrDefault();
            var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO media_access.video_transcription_events (event_id, tenant_id, received_at)
                VALUES ({notification.EventId}, {attempt?.TenantId ?? Guid.Empty}, {notification.ReceivedAt})
                ON CONFLICT (event_id) DO NOTHING
                """, cancellationToken);
            if (inserted != 0) await change(attempt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (NpgsqlException exception) when (exception.IsTransient) { return false; }
        catch (DbUpdateException exception) when (exception.InnerException is NpgsqlException { IsTransient: true }) { return false; }
        catch (TimeoutException) { return false; }
    }
}
