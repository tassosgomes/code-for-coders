namespace CodeForCoders.Media.Infra.Data.Transcriptions;

// Independent of the attempt: restarting must not discard its pending storage cleanup.
public sealed class TranscriptionAudioCleanup
{
    public Guid TranscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid VideoId { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
}
