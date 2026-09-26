using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.Videos.GetVideo;

public sealed class GetVideo(IVideoQueries videoQueries) : IGetVideo
{
    public async Task<VideoOutput?> ExecuteAsync(GetVideoInput input, CancellationToken cancellationToken)
    {
        var video = await videoQueries.GetAsync(input.VideoId, cancellationToken);
        return video is null
            ? null
            : new VideoOutput(
                video.VideoId,
                video.Title,
                video.Status,
                new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
                video.UploadedAt,
                video.DurationSeconds,
                video.FailureReason);
    }
}
