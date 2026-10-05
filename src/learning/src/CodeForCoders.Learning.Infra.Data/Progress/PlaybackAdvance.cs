namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class PlaybackAdvance
{
    private PlaybackAdvance() { }

    public Guid EventId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid LessonId { get; private set; }
    public int Sequence { get; private set; }
    public int PositionSeconds { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
}
