namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoTranscoder
{
    Task<VideoTranscodeResult> TranscodeAsync(
        Guid videoId,
        string sourcePath,
        string outputDirectory,
        string keyInfoPath,
        CancellationToken cancellationToken);
}

public sealed record VideoTranscodeResult(
    int DurationSeconds,
    IReadOnlyList<VideoQuality> Qualities,
    long StoredBytes,
    string? FailureReason = null)
{
    public static VideoTranscodeResult Failed(string reason)
        => new(0, [], 0, reason);
}

public sealed record VideoQuality(string Name, int Width, int Height, long Bandwidth);
