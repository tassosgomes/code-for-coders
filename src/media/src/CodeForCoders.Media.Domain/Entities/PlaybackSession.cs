namespace CodeForCoders.Media.Domain.Entities;

public sealed class PlaybackSession
{
    private PlaybackSession() { }
    public Guid SessionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid VideoId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool TryRenew(DateTimeOffset now)
    {
        if (ExpiresAt <= now) return false;
        ExpiresAt = now.AddMinutes(5);
        return true;
    }

    public static PlaybackSession Create(PlaybackSessionCreateInput input)
    {
        if (input.TenantId == Guid.Empty || input.StudentId == Guid.Empty || input.LessonId == Guid.Empty
            || input.CourseId == Guid.Empty || input.VideoId == Guid.Empty)
            throw new CodeForCoders.Media.Domain.SeedWork.EntityValidationException("Playback session identifiers are required.");
        return new()
        {
            SessionId = Guid.CreateVersion7(input.Now),
            TenantId = input.TenantId,
            StudentId = input.StudentId,
            LessonId = input.LessonId,
            CourseId = input.CourseId,
            VideoId = input.VideoId,
            CreatedAt = input.Now,
            ExpiresAt = input.Now.AddMinutes(5),
        };
    }
}
