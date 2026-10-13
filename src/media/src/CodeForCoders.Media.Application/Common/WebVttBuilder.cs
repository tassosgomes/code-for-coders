using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;

namespace CodeForCoders.Media.Application.Common;

public static partial class WebVttBuilder
{
    private const int CharactersPerLine = 42;
    private const int CharactersPerCue = CharactersPerLine * 2;
    private const long MinimumDurationMs = 500;

    public static string Build(IReadOnlyList<TranscriptSegmentOutput> segments)
    {
        // Group equal starts before cutting overlaps so no accepted text is lost.
        var groups = segments.OrderBy(segment => segment.StartMs).GroupBy(segment => segment.StartMs)
            .Select(group => new TranscriptSegmentOutput(group.Key, group.Max(segment => segment.EndMs),
                Sanitize(string.Join(" ", group.Select(segment => segment.Text))))).ToArray();
        var output = new StringBuilder("WEBVTT\n\n");
        var number = 0;
        for (var index = 0; index < groups.Length; index++)
        {
            var segment = groups[index];
            var nextStart = index + 1 < groups.Length ? groups[index + 1].StartMs : long.MaxValue;
            var end = Math.Min(Math.Max(segment.EndMs, segment.StartMs + MinimumDurationMs), nextStart);
            AppendSegment(output, segment with { EndMs = end }, ref number);
        }
        return output.ToString();
    }

    private static void AppendSegment(StringBuilder output, TranscriptSegmentOutput segment, ref int number)
    {
        // Text elements keep surrogate pairs and multi-codepoint emoji intact.
        var offsets = StringInfo.ParseCombiningCharacters(segment.Text);
        var length = offsets.Length;
        var window = segment.EndMs - segment.StartMs;
        var parts = (int)Math.Max(1, Math.Min((length + CharactersPerCue - 1) / CharactersPerCue, window / MinimumDurationMs));
        for (var part = 0; part < parts; part++)
        {
            var startCharacter = part * length / parts;
            var endCharacter = (part + 1) * length / parts;
            var textStart = startCharacter < length ? offsets[startCharacter] : segment.Text.Length;
            var textEnd = endCharacter < length ? offsets[endCharacter] : segment.Text.Length;
            var start = segment.StartMs + (length == 0 ? 0 : window * startCharacter / length);
            var end = part + 1 == parts ? segment.EndMs : segment.StartMs + window * endCharacter / length;
            var text = segment.Text[textStart..textEnd];
            output.Append(++number).Append('\n').Append(Timestamp(start)).Append(" --> ").Append(Timestamp(end)).Append('\n')
                .Append(Escape(Wrap(text))).Append("\n\n");
        }
    }

    private static string Wrap(string text)
    {
        var offsets = StringInfo.ParseCombiningCharacters(text);
        if (offsets.Length <= CharactersPerLine || offsets.Length > CharactersPerCue) return text;
        var middle = offsets.Length / 2;
        var boundary = offsets[middle];
        // Prefer a word boundary near the middle, within the two-line budget.
        var first = Math.Max(1, offsets.Length - CharactersPerLine);
        var last = Math.Min(CharactersPerLine, offsets.Length - 1);
        var wordBoundary = Enumerable.Range(first, last - first + 1)
            .Where(index => text[offsets[index]] == ' ').OrderBy(index => Math.Abs(index - middle)).FirstOrDefault();
        if (wordBoundary > 0) boundary = offsets[wordBoundary];
        return text[..boundary] + "\n" + text[boundary..];
    }

    private static string Sanitize(string text) => Whitespace().Replace(text.Replace("-->", string.Empty, StringComparison.Ordinal), " ").Trim();
    private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    private static string Timestamp(long milliseconds) => string.Create(CultureInfo.InvariantCulture,
        $"{milliseconds / 3_600_000:00}:{milliseconds / 60_000 % 60:00}:{milliseconds / 1000 % 60:00}.{milliseconds % 1000:000}");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
