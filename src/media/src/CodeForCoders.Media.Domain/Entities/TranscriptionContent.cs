using System.Text.Json;
using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Domain.Entities;

public sealed class TranscriptionContent
{
    private TranscriptionContent() { }
    public Guid TranscriptionId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Language { get; private set; } = string.Empty;
    public long DurationMs { get; private set; }
    public string SegmentsJson { get; private set; } = "[]";

    public static TranscriptionContent Create(TranscriptionContentInput input)
    {
        if (input.TranscriptionId == Guid.Empty || input.TenantId == Guid.Empty || input.DurationMs < 0
            || string.IsNullOrWhiteSpace(input.Language) || input.Segments.Count > 20_000
            || input.Segments.Any(segment => segment.StartMs < 0 || segment.EndMs < segment.StartMs || string.IsNullOrWhiteSpace(segment.Text)))
            throw new EntityValidationException("Transcription content is invalid.");
        return new TranscriptionContent
        {
            TranscriptionId = input.TranscriptionId,
            TenantId = input.TenantId,
            Language = input.Language,
            DurationMs = input.DurationMs,
            SegmentsJson = JsonSerializer.Serialize(input.Segments, JsonSerializerOptions.Web),
        };
    }
}
