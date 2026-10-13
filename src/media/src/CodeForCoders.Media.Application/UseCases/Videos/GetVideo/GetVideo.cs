using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideo;

public sealed class GetVideo(IVideoQueries videoQueries, IVideoTagRepository videoTags, IVideoTranscriptionRepository transcriptions) : IGetVideo
{
    public async Task<VideoOutput?> ExecuteAsync(GetVideoInput input, CancellationToken cancellationToken)
    {
        var video = await videoQueries.GetAsync(input.VideoId, cancellationToken);
        var tags = video is null ? new Dictionary<Guid, IReadOnlyList<string>>() : await videoTags.GetAsync([video.VideoId], cancellationToken);
        var states = await transcriptions.GetAsync(video is null ? [] : [video], cancellationToken);
        return video is null
            ? null
            : new VideoOutput(
                video.VideoId,
                video.Title,
                video.Status,
                new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
                video.UploadedAt,
                video.DurationSeconds,
                video.FailureReason, tags.GetValueOrDefault(video.VideoId, []), VideoTranscriptionOutput.FromState(states[video.VideoId]));
    }
}
