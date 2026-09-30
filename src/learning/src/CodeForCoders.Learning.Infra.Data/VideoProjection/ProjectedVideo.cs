namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class ProjectedVideo
{
    public Guid TenantId { get; private set; }
    public Guid VideoId { get; private set; }
    public bool IsReady { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid EventId { get; private set; }
}
