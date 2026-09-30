namespace CodeForCoders.Learning.Infra.Data.VideoProjection;

public sealed class VideoFactReceipt
{
    public Guid EventId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
}
