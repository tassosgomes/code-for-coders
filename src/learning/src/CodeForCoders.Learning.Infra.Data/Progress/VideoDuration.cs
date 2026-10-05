namespace CodeForCoders.Learning.Infra.Data.Progress;

public sealed class VideoDuration
{
    private VideoDuration() { }

    public Guid TenantId { get; private set; }
    public Guid VideoId { get; private set; }
    public int DurationSeconds { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid EventId { get; private set; }
}
