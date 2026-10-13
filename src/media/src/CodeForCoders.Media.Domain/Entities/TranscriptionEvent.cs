namespace CodeForCoders.Media.Domain.Entities;

public sealed class TranscriptionEvent
{
    private TranscriptionEvent() { }
    public string EventId { get; private set; } = string.Empty;
    public Guid TenantId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
}
