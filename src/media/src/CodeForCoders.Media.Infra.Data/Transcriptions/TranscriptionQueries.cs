using CodeForCoders.Media.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.Transcriptions;

public sealed class TranscriptionQueries(MediaDbContext dbContext) : ITranscriptionQueries
{
    public Task<TranscriptionContentSnapshot?> GetCurrentAsync(Guid videoId, CancellationToken cancellationToken)
        => (from attempt in dbContext.Transcriptions.AsNoTracking()
            join content in dbContext.TranscriptionContents.AsNoTracking()
                on attempt.TranscriptionId equals content.TranscriptionId
            where attempt.VideoId == videoId && attempt.IsCurrent && attempt.Status == "ready" && attempt.ReadyAt != null
            select new TranscriptionContentSnapshot(attempt.TranscriptionId, content.Language,
                content.DurationMs, attempt.ReadyAt!.Value, content.SegmentsJson))
            .SingleOrDefaultAsync(cancellationToken);
}
