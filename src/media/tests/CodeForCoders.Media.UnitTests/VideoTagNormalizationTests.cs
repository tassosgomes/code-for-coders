using CodeForCoders.Media.Domain.Exceptions;
using CodeForCoders.Media.Domain.ValueObjects;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class VideoTagNormalizationTests
{
    [Fact]
    public void EquivalentTagsKeepFirstDisplayName()
    {
        var tags = VideoTagSet.Normalize([" Dócker ", "docker", "DOCKER"]);
        Assert.Equal("Dócker", Assert.Single(tags).DisplayName);
        Assert.Equal("docker", tags[0].NormalizedName);
    }
    [Fact]
    public void WhitespaceIsTrimmedAndCollapsed()
        => Assert.Equal("Módulo 2", Assert.Single(VideoTagSet.Normalize(["  Módulo   2  "])).DisplayName);
    [Fact]
    public void EmptySetIsValid() => Assert.Empty(VideoTagSet.Normalize([]));
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abcdefghijklmnopqrstuvwxzy12345")]
    [InlineData("bad\nvalue")]
    [InlineData("\u0301")]
    public void InvalidTagIsRejected(string tag)
        => Assert.Equal("TAG_INVALID", Assert.Throws<VideoTagRuleViolationException>(() => VideoTagSet.Normalize([tag])).Code);
    [Fact]
    public void ElevenDistinctTagsAreRejected()
        => Assert.Equal("TAGS_LIMIT_EXCEEDED", Assert.Throws<VideoTagRuleViolationException>(() =>
            VideoTagSet.Normalize(Enumerable.Range(1, 11).Select(index => $"Tag {index}").ToArray())).Code);
    [Fact]
    public void DuplicateTagsDoNotCountAgainstLimit()
        => Assert.Single(VideoTagSet.Normalize(Enumerable.Repeat("docker", 50).ToArray()));
    [Fact]
    public void OrderIgnoresAccentsAndCase()
        => Assert.Equal(new[] { "Álgebra", "docker", "Zebra" }, VideoTagSet.Normalize(["Zebra", "docker", "Álgebra"]).Select(tag => tag.DisplayName));
}
