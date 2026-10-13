namespace CodeForCoders.Media.Application.UseCases.Videos;

public sealed record VideoUploaderOutput(Guid AccountId, string Name);

public sealed record VideoOutput(
    Guid VideoId,
    string Title,
    string Status,
    VideoUploaderOutput UploadedBy,
    DateTimeOffset UploadedAt,
    int? DurationSeconds,
    string? FailureReason,
    IReadOnlyList<string>? Tags = null,
    VideoTranscriptionOutput? Transcription = null)
{
    public VideoTranscriptionOutput Transcription { get; init; } = Transcription ?? new("not-requested", false, null, null, false);

    public IReadOnlyList<string> Tags { get; init; } = Tags ?? [];
}

public sealed record VideoPaginationOutput(int Page, int Size, long Total, long TotalPages);

public sealed record VideoPageOutput(IReadOnlyList<VideoOutput> Data, VideoPaginationOutput Pagination);
