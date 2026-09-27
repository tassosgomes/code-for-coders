using Xunit;

namespace CodeForCoders.Media.IntegrationTests;

public sealed class FfmpegAvailabilityTests
{
    [Fact]
    public async Task FfmpegAndFfprobeRespondToVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var (ffmpeg, ffprobe) = await FfmpegTools.EnsureAvailableAsync(cancellationToken);

        var ffmpegVersion = await FfmpegTools.ReadVersionAsync(ffmpeg, cancellationToken);
        var ffprobeVersion = await FfmpegTools.ReadVersionAsync(ffprobe, cancellationToken);

        Assert.StartsWith("ffmpeg version ", ffmpegVersion);
        Assert.StartsWith("ffprobe version ", ffprobeVersion);
    }
}
