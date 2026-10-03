using System.Text.RegularExpressions;

namespace CodeForCoders.Media.Application.Common;

public static partial class PlaylistRewriter
{
    public static string Rewrite(string original, Guid videoId, Uri segmentBase)
        => string.Join("\n", original.Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(line => RewriteLine(line, videoId, segmentBase)));

    private static string RewriteLine(string line, Guid videoId, Uri segmentBase)
    {
        if (line.Contains("c4c-key:", StringComparison.Ordinal))
        {
            if (!line.Contains($"c4c-key:{videoId:D}", StringComparison.Ordinal))
                throw new InvalidOperationException("The playlist key does not match its video.");
            return line.Replace($"c4c-key:{videoId:D}", "../key", StringComparison.Ordinal);
        }
        if (line.Length == 0 || line.StartsWith('#')) return line;
        if (VariantPattern().IsMatch(line)) return "variants/" + line[..^5];
        if (SegmentPattern().IsMatch(line)) return new Uri(segmentBase, line).AbsoluteUri;
        throw new InvalidOperationException("The playlist contains an unsupported resource.");
    }

    public static bool IsQuality(string quality) => QualityPattern().IsMatch(quality);

    [GeneratedRegex(@"^(480p|720p|1080p)\.m3u8$", RegexOptions.CultureInvariant)]
    private static partial Regex VariantPattern();
    [GeneratedRegex(@"^(480p|720p|1080p)/segment_[0-9]+\.ts$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
    [GeneratedRegex(@"^(480p|720p|1080p)$", RegexOptions.CultureInvariant)]
    private static partial Regex QualityPattern();
}
