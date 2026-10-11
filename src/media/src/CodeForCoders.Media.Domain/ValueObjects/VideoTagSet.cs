using CodeForCoders.Media.Domain.Entities;
using CodeForCoders.Media.Domain.Exceptions;

namespace CodeForCoders.Media.Domain.ValueObjects;

public static class VideoTagSet
{
    public const int MaximumTags = 10;
    public const int MaximumLength = 30;

    public static IReadOnlyList<VideoTagValue> Normalize(IReadOnlyList<string> values)
    {
        var tags = new Dictionary<string, VideoTagValue>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value is null || value.Any(char.IsControl))
                throw new VideoTagRuleViolationException("TAG_INVALID", "A tag must not contain control characters.");
            var display = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (display.Length is < 1 or > MaximumLength)
                throw new VideoTagRuleViolationException("TAG_INVALID", "A tag must contain between 1 and 30 characters.");
            var normalized = Video.NormalizeSearchTitle(display);
            if (normalized.Length == 0)
                throw new VideoTagRuleViolationException("TAG_INVALID", "A tag must contain a searchable character.");
            tags.TryAdd(normalized, new VideoTagValue(display, normalized));
        }
        if (tags.Count > MaximumTags)
            throw new VideoTagRuleViolationException("TAGS_LIMIT_EXCEEDED", "A video may have at most 10 distinct tags.");
        return tags.Values.OrderBy(tag => tag.NormalizedName, StringComparer.Ordinal).ToArray();
    }
}
