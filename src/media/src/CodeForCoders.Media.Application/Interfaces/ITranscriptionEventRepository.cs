using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface ITranscriptionEventRepository
{
    Task<bool> RecordAsync(TranscriptionNotification notification,
        Func<Transcription?, CancellationToken, Task> change, CancellationToken cancellationToken);
}
