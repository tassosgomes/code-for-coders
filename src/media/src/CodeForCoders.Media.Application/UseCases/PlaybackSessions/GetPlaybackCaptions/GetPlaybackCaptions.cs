using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;
using Microsoft.Extensions.Options;

namespace CodeForCoders.Media.Application.UseCases.PlaybackSessions.GetPlaybackCaptions;

public sealed class GetPlaybackCaptions(IPlaybackRepository sessions, ITranscriptionQueries transcriptions,
    TimeProvider clock, IOptions<TranscriptionOptions> options) : IGetPlaybackCaptions
{
    public async Task<PlaybackCaptionsOutput> ExecuteAsync(GetPlaybackCaptionsInput input, CancellationToken cancellationToken)
    {
        var session = await sessions.FindSessionAsync(input.SessionId, cancellationToken);
        if (session is null || session.StudentId != input.StudentId || session.TenantId != input.TenantId)
            throw new MediaApiException(404, "PLAYBACK_SESSION_NOT_FOUND", "Playback session was not found.");
        if (session.ExpiresAt <= clock.GetUtcNow())
            throw new MediaApiException(410, "PLAYBACK_SESSION_EXPIRED", "Playback session has expired.");
        var current = options.Value.Enabled ? await transcriptions.GetCurrentAsync(session.VideoId, cancellationToken) : null;
        if (current is null)
            throw new MediaApiException(404, "CAPTIONS_NOT_AVAILABLE", "Captions are not available for this video.");
        var segments = JsonSerializer.Deserialize<TranscriptSegmentOutput[]>(current.SegmentsJson, JsonSerializerOptions.Web)!;
        return new(WebVttBuilder.Build(segments));
    }
}
