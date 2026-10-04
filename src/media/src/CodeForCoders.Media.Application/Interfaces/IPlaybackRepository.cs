using CodeForCoders.Media.Domain.Entities;

namespace CodeForCoders.Media.Application.Interfaces;

public interface IPlaybackRepository
{
    Task<PlaybackLesson?> FindLessonAsync(Guid lessonId, CancellationToken cancellationToken);
    Task<PlaybackSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<PlaybackVideoKey?> FindVideoKeyAsync(Guid videoId, CancellationToken cancellationToken);
    Task<int?> FindVideoDurationAsync(Guid videoId, CancellationToken cancellationToken);
    void Add(PlaybackSession session);
}
