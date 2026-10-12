using CodeForCoders.Media.Application.Interfaces;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Exceptions;
using FluentValidation;
using CodeForCoders.Media.Application.UseCases;

namespace CodeForCoders.Media.Application.UseCases.Videos.ListVideos;

public sealed class ListVideos(IVideoQueries videoQueries, IVideoTagRepository videoTags, IValidator<ListVideosInput> validator) : IListVideos
{
    public async Task<VideoPageOutput> ExecuteAsync(ListVideosInput input, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(input, cancellationToken);
        if (!validation.IsValid)
            throw new MediaApiException(400, "INVALID_REQUEST", "The video filters are invalid.");
        var snapshot = await videoQueries.ListAsync(
            new(input.Page, input.Size, input.Statuses, input.Query, input.Tags ?? [], input.TagMatch), cancellationToken);
        MediaTelemetry.VideosListed.Add(1,
            new KeyValuePair<string, object?>("tag_filter", input.Tags?.Count > 0 ? input.TagMatch : "none"),
            new KeyValuePair<string, object?>("ready_only", input.Statuses.Count == 1 && input.Statuses[0] == "ready"));
        var tags = await videoTags.GetAsync(snapshot.Data.Select(video => video.VideoId).ToArray(), cancellationToken);
        var data = snapshot.Data.Select(video => new VideoOutput(
            video.VideoId,
            video.Title,
            video.Status,
            new VideoUploaderOutput(video.UploadedByAccountId, video.UploadedByName),
            video.UploadedAt,
            video.DurationSeconds,
            video.FailureReason, tags.GetValueOrDefault(video.VideoId, []))).ToArray();
        var totalPages = snapshot.Total == 0 ? 0 : (snapshot.Total + input.Size - 1) / input.Size;

        return new VideoPageOutput(
            data,
            new VideoPaginationOutput(input.Page, input.Size, snapshot.Total, totalPages));
    }
}
