namespace CodeForCoders.Media.Application.UseCases.Videos;

public sealed record VideoUploaderOutput(Guid AccountId, string Name);

public sealed record VideoOutput(
    Guid VideoId,
    string Title,
    string Status,
    VideoUploaderOutput UploadedBy,
    DateTimeOffset UploadedAt,
    int? DurationSeconds,
    string? FailureReason);

public sealed record VideoPaginationOutput(int Page, int Size, long Total, long TotalPages);

public sealed record VideoPageOutput(IReadOnlyList<VideoOutput> Data, VideoPaginationOutput Pagination);
