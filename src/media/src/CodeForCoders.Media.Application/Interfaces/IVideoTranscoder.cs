namespace CodeForCoders.Media.Application.Interfaces;

public interface IVideoTranscoder
{
    Task<VideoTranscodeResult> TranscodeAsync(
        string sourcePath,
        string outputDirectory,
        string keyInfoPath,
        CancellationToken cancellationToken);
}

public sealed record VideoTranscodeResult(int DurationSeconds, IReadOnlyList<VideoQuality> Qualities, long StoredBytes);

public sealed record VideoQuality(string Name, int Width, int Height, long Bandwidth);
