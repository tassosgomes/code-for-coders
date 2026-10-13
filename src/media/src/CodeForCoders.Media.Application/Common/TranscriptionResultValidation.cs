using System.Text.Json;
using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.SeedWork;

namespace CodeForCoders.Media.Application.Common;

public static class TranscriptionResultValidation
{
    public const int MaximumJsonBytes = 8 * 1024 * 1024;
    public const int MaximumSegments = 20_000;

    public static TranscriptionContent Validate(Guid transcriptionId, Guid tenantId, byte[] json)
    {
        if (json.Length > MaximumJsonBytes) throw InvalidResult();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1) throw InvalidResult();
            var segments = root.GetProperty("segments");
            if (segments.GetArrayLength() > MaximumSegments) throw InvalidResult();
            var normalized = segments.EnumerateArray().Select(ReadSegment)
                .Where(segment => segment.Text.Length > 0).OrderBy(segment => segment.StartMs).ToArray();
            var content = TranscriptionContent.Create(new(transcriptionId, tenantId,
                root.GetProperty("language").GetString()!, root.GetProperty("durationMs").GetInt64(), normalized));
            if (System.Text.Encoding.UTF8.GetByteCount(content.SegmentsJson) > MaximumJsonBytes) throw InvalidResult();
            return content;
        }
        catch (JsonException) { throw InvalidResult(); }
        catch (KeyNotFoundException) { throw InvalidResult(); }
        catch (InvalidOperationException) { throw InvalidResult(); }
        catch (FormatException) { throw InvalidResult(); }
        catch (OverflowException) { throw InvalidResult(); }
    }

    private static TranscriptionSegment ReadSegment(JsonElement segment)
    {
        var start = segment.GetProperty("startMs").GetInt64();
        var end = segment.GetProperty("endMs").GetInt64();
        if (start < 0 || end < start) throw InvalidResult();
        return new(start, end, segment.GetProperty("text").GetString()?.Trim() ?? throw InvalidResult());
    }

    private static EntityValidationException InvalidResult() => new("The transcription result is invalid.");
}
