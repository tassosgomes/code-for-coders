namespace CodeForCoders.Media.Domain.Entities;

public sealed class TranscriptionContent
{
    private TranscriptionContent() { }
    public Guid TranscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public long DurationMs { get; private set; }
    public string SegmentsJson { get; private set; } = "[]";
}
