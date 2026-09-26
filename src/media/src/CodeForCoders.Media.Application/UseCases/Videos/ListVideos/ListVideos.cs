using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideos;

public sealed class ListVideos(IVideoQueries videoQueries) : IListVideos
{
    public async Task<VideoPageOutput> ExecuteAsync(ListVideosInput input, CancellationToken cancellationToken)
    {
        var snapshot = await videoQueries.ListAsync(input.Page, input.Size, cancellationToken);
        var data = snapshot.Data.Select(video => new VideoOutput(
            video.VideoId,
            video.Title,
            video.Status,
            new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
            video.UploadedAt,
            video.DurationSeconds,
            video.FailureReason)).ToArray();
        var totalPages = snapshot.Total == 0 ? 0 : (snapshot.Total + input.Size - 1) / input.Size;

        return new VideoPageOutput(
            data,
            new VideoPaginationOutput(input.Page, input.Size, snapshot.Total, totalPages));
    }
}
