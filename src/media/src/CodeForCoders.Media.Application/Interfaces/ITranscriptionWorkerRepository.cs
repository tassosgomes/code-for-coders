using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionWorkerRepository
{
    Task<TranscriptionWorkItem?> ClaimNextAsync(TranscriptionClaim claim, CancellationToken cancellationToken);
    Task<bool> WithLeaseAsync(TranscriptionWorkItem work, Func<Transcription, CancellationToken, Task> change, CancellationToken cancellationToken);
    Task<bool> RenewLeaseAsync(TranscriptionLeaseRenewal renewal, CancellationToken cancellationToken);
    Task MarkCleanupAsync(Guid transcriptionId, DateTimeOffset now, CancellationToken cancellationToken);
    Task DeleteOldEventsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
    Task ReplaceContentAsync(TranscriptionContent content, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranscriptionWorkItem>> GetExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<TranscriptionWorkItem>> GetAudioCleanupAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TranscriptionAudioCleanupItem>> GetPendingAudioCleanupAsync(CancellationToken cancellationToken);
}
