namespace CodeForCoders.BffAdmin.Api.ApiModels;

public sealed record VideoUploaderResponse(Guid AccountId, string Name);

public sealed record VideoResponse(
    Guid VideoId,
    string Title,
    string Status,
    VideoUploaderResponse UploadedBy,
    DateTimeOffset UploadedAt,
    int? DurationSeconds,
    string? FailureReason);

public sealed record VideoPaginationResponse(int Page, int Size, long Total, long TotalPages);

public sealed record VideoPageResponse(IReadOnlyList<VideoResponse> Data, VideoPaginationResponse Pagination);
