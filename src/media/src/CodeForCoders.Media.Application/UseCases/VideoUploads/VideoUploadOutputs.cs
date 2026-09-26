using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Application.UseCases.Videos;

namespace CodeForCoders.Media.Application.UseCases.VideoUploads;

public sealed record VideoUploadOutput(
    Guid UploadId,
    string Title,
    string FileName,
    long FileSize,
    long PartSize,
    int PartCount,
    IReadOnlyList<int> ReceivedParts,
    DateTimeOffset ExpiresAt)
{
    public static VideoUploadOutput From(VideoUpload upload, IReadOnlyList<int> receivedParts)
        => new(
            upload.UploadId,
            upload.Title,
            upload.FileName,
            upload.FileSize,
            VideoUpload.PartSizeBytes,
            upload.PartCount,
            receivedParts,
            upload.ExpiresAt);
}

public sealed record VideoUploadPaginationOutput(int Page, int Size, long Total, long TotalPages);

public sealed record VideoUploadPageOutput(
    IReadOnlyList<VideoUploadOutput> Data,
    VideoUploadPaginationOutput Pagination);

public sealed record VideoPartUrlOutput(int PartNumber, Uri Url, DateTimeOffset ExpiresAt);

public sealed record VideoPartUrlsOutput(
    IReadOnlyList<VideoPartUrlOutput> Parts,
    DateTimeOffset UploadExpiresAt);

public sealed record CompletedVideoOutput(VideoOutput Video);
