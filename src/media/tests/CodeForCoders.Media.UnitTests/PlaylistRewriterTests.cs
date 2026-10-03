using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.Interfaces;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class PlaylistRewriterTests
{
    [Theory(DisplayName = nameof(MasterUsesSessionRelativeVariants))]
    [InlineData("480p")]
    [InlineData("720p")]
    [InlineData("1080p")]
    public void MasterUsesSessionRelativeVariants(string quality)
    {
        var result = PlaylistRewriter.Rewrite("#EXTM3U\n" + quality + ".m3u8\n", Guid.CreateVersion7(), new Uri("https://edge.test/video/hls/"));
        Assert.Contains("variants/" + quality, result);
        Assert.DoesNotContain(".m3u8", result);
    }

    [Fact(DisplayName = nameof(VariantUsesSessionKeyAndCredentialFreeSegments))]
    public void VariantUsesSessionKeyAndCredentialFreeSegments()
    {
        var video = Guid.CreateVersion7();
        var result = PlaylistRewriter.Rewrite($"#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"c4c-key:{video:D}\"\n480p/segment_00001.ts\n", video, new Uri("https://edge.test/video/hls/"));
        Assert.Contains("URI=\"../key\"", result);
        Assert.Contains("https://edge.test/video/hls/480p/segment_00001.ts", result);
        Assert.DoesNotContain("?", result);
    }

    [Fact(DisplayName = nameof(ForeignVideoKeyIsRejected))]
    public void ForeignVideoKeyIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PlaylistRewriter.Rewrite($"#EXT-X-KEY:URI=\"c4c-key:{Guid.CreateVersion7():D}\"", Guid.CreateVersion7(), new Uri("https://edge.test/")));

    [Fact(DisplayName = nameof(TraversalAndExternalSegmentsAreRejected))]
    public void TraversalAndExternalSegmentsAreRejected()
    {
        foreach (var path in new[] { "../other/segment.ts", "https://other.test/segment.ts", "/other/segment.ts" })
            Assert.Throws<InvalidOperationException>(() => PlaylistRewriter.Rewrite(path, Guid.CreateVersion7(), new Uri("https://edge.test/")));
    }
}
