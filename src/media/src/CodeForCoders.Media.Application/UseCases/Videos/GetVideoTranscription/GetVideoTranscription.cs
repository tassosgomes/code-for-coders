using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using Microsoft.Extensions.Options;
using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;

namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideoTranscription;

public sealed class GetVideoTranscription(
    IVideoQueries videos, ITranscriptionQueries transcriptions, IOptions<TranscriptionOptions> options) : IGetVideoTranscription
{
    public async Task<VideoTranscriptionContentOutput> ExecuteAsync(GetVideoTranscriptionInput input, CancellationToken cancellationToken)
    {
        var video = await videos.GetAsync(input.VideoId, cancellationToken);
        if (video is null)
            throw new MediaApiException(404, "VIDEO_NOT_FOUND", "The requested video was not found.");
        var current = options.Value.Enabled && video.Status == "ready"
            ? await transcriptions.GetCurrentAsync(input.VideoId, cancellationToken) : null;
        if (current is null)
            throw new MediaApiException(409, "TRANSCRIPTION_NOT_READY", "This video has no ready transcription.");
        return new(input.VideoId, "tr_" + current.TranscriptionId.ToString("N")[^8..], current.Language,
            current.DurationMs, current.ReadyAt,
            JsonSerializer.Deserialize<TranscriptSegmentOutput[]>(current.SegmentsJson, JsonSerializerOptions.Web)!);
    }
}
