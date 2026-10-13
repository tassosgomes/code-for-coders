using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoTranscriptionRepository
{
    Task<IReadOnlyDictionary<Guid, TranscriptionState>> GetAsync(IReadOnlyList<Video> videos, CancellationToken cancellationToken);
    Task<bool> WithRequestAsync(VideoTranscriptionWriteScope scope, Func<Video, CancellationToken, Task> request, CancellationToken cancellationToken);
    Task EnqueueAsync(Transcription attempt, CancellationToken cancellationToken);
    Task WithAutomaticEnqueueAsync(Transcription attempt, Func<CancellationToken, Task> commitPreparation, CancellationToken cancellationToken);
}
