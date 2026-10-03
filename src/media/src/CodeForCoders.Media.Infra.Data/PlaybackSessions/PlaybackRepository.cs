using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodeForCoders.Media.Infra.Data.PlaybackSessions;

public sealed class PlaybackRepository(MediaDbContext context) : IPlaybackRepository
{
    public Task<PlaybackLesson?> FindLessonAsync(Guid lessonId, CancellationToken cancellationToken)
        => (from reference in context.CourseVideoReferences.AsNoTracking()
            join video in context.Videos.AsNoTracking() on reference.VideoId equals video.VideoId
            where reference.LessonId == lessonId
            select new PlaybackLesson(reference.CourseId, video.VideoId, video.Status)).SingleOrDefaultAsync(cancellationToken);
    public Task<PlaybackSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
        => context.PlaybackSessions.AsNoTracking().SingleOrDefaultAsync(session => session.SessionId == sessionId, cancellationToken);
    public Task<PlaybackVideoKey?> FindVideoKeyAsync(Guid videoId, CancellationToken cancellationToken)
        => context.Videos.AsNoTracking().Where(video => video.VideoId == videoId && video.Status == "ready")
            .Select(video => new PlaybackVideoKey(video.VideoId, video.MasterKeyId!, video.EncryptedVideoKey!))
            .SingleOrDefaultAsync(cancellationToken);
    public void Add(PlaybackSession session) => context.PlaybackSessions.Add(session);
}
