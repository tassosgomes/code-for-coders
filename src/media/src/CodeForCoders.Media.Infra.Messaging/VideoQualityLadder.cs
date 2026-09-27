using CodeForCoders.Media.Application.Interfaces;

namespace CodeForCoders.Media.Infra.Messaging;

public static class VideoQualityLadder
{
    public const long Bandwidth1080p = 5_000_000L;
    public const long Bandwidth720p = 2_800_000L;
    public const long Bandwidth480p = 1_200_000L;
    public const long BandwidthBelow480p = 800_000L;

    public static IReadOnlyList<VideoQuality> Select(int sourceWidth, int sourceHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceHeight, 1);

        var heights = sourceHeight switch
        {
            >= 1080 => new[] { 1080, 720, 480 },
            >= 720 => new[] { 720, 480 },
            >= 480 => new[] { 480 },
            _ => new[] { Math.Max(2, sourceHeight - (sourceHeight % 2)) },
        };
        return heights.Select(height =>
        {
            var width = Math.Max(2, (int)Math.Round(sourceWidth * (double)height / sourceHeight / 2) * 2);
            var bandwidth = height switch
            {
                1080 => Bandwidth1080p,
                720 => Bandwidth720p,
                480 => Bandwidth480p,
                _ => BandwidthBelow480p,
            };
            return new VideoQuality($"{height}p", width, height, bandwidth);
        }).ToArray();
    }
}
