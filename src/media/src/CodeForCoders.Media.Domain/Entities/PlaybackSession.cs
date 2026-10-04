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
    public int? LastSequence { get; private set; }
    public DateTimeOffset? LastProgressAt { get; private set; }
    public int? LastPositionSeconds { get; private set; }

    public bool TryRenew(DateTimeOffset now)
    {
        if (ExpiresAt <= now) return false;
        ExpiresAt = now.AddMinutes(5);
        return true;
    }

    public bool TryRecordProgress(int sequence, DateTimeOffset now, int positionSeconds, int minGapSeconds = 10)
    {
        if (LastSequence.HasValue && sequence <= LastSequence.Value)
        {
            return false;
        }

        if (LastProgressAt.HasValue && (now - LastProgressAt.Value).TotalSeconds < minGapSeconds)
        {
            return false;
        }

        LastSequence = sequence;
        LastProgressAt = now;
        LastPositionSeconds = positionSeconds;
        return true;
    }

    public static Guid CreateDeterministicEventId(Guid sessionId, int sequence)
    {
        var input = $"{sessionId:D}:{sequence}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
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
