namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class LessonProgress
{
    private LessonProgress() { }

    public Guid TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid CourseId { get; private set; }
    public int LastPositionSeconds { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public int Sequence { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public int MaxPositionSeconds { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset LastActivityAt { get; private set; }
}
