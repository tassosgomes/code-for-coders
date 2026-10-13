using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Application.UseCases.Transcriptions.Common;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class WebVttBuilderTests
{
    [Fact(DisplayName = nameof(EmptyTranscriptHasOnlyWebVttHeader))]
    public void EmptyTranscriptHasOnlyWebVttHeader() => Assert.Equal("WEBVTT\n\n", WebVttBuilder.Build([]));

    [Fact(DisplayName = nameof(EscapesMarkupAndRemovesCueArrow))]
    public void EscapesMarkupAndRemovesCueArrow()
    {
        var result = Build(0, 1000, "A & <script> --> B");
        Assert.Contains("A &amp; &lt;script&gt; B", result);
        Assert.Single(result.Split(" --> ").Skip(1));
    }

    [Fact(DisplayName = nameof(CollapsesBlankLinesWithoutLosingWords))]
    public void CollapsesBlankLinesWithoutLosingWords()
        => Assert.Contains("First second third\n\n", Build(0, 1000, "First\r\n\r\nsecond\n\nthird"));

    [Fact(DisplayName = nameof(FormatsThreeMinuteCueAndMillisecondPrecision))]
    public void FormatsThreeMinuteCueAndMillisecondPrecision()
        => Assert.Contains("01:00:00.123 --> 01:03:00.456", Build(3_600_123, 3_780_456, "A three minute cue"));

    [Fact(DisplayName = nameof(SplitsLongTextIntoAtMostTwoLinesPerCue))]
    public void SplitsLongTextIntoAtMostTwoLinesPerCue()
    {
        var cues = Cues(Build(0, 2000, new string('a', 168)));
        Assert.Equal(2, cues.Length);
        foreach (var cue in cues)
        {
            var lines = cue.Split('\n').Skip(2).ToArray();
            Assert.Equal(2, lines.Length); Assert.All(lines, line => Assert.Equal(42, line.Length));
        }
        Assert.Contains("00:00:00.000 --> 00:00:01.000", cues[0]);
        Assert.Contains("00:00:01.000 --> 00:00:02.000", cues[1]);
    }

    [Fact(DisplayName = nameof(SplitTimesFollowCharacterProportions))]
    public void SplitTimesFollowCharacterProportions()
    {
        var cues = Cues(Build(0, 1700, new string('a', 85)));
        Assert.Equal(2, cues.Length);
        Assert.Contains("00:00:00.000 --> 00:00:00.840", cues[0]);
        Assert.Contains("00:00:00.840 --> 00:00:01.700", cues[1]);
    }

    [Fact(DisplayName = nameof(EmojiSequencesRemainIntactAcrossCueAndLineBoundaries))]
    public void EmojiSequencesRemainIntactAcrossCueAndLineBoundaries()
    {
        const string emoji = "👩🏽‍💻";
        var result = Build(0, 2000, string.Concat(Enumerable.Repeat(emoji, 168)));
        Assert.Equal(2, Cues(result).Length);
        var restored = string.Concat(Cues(result).SelectMany(cue => cue.Split('\n').Skip(2)));
        Assert.Equal(string.Concat(Enumerable.Repeat(emoji, 168)), restored);
    }

    [Fact(DisplayName = nameof(CutsOverlappingCueAtNextReceivedStart))]
    public void CutsOverlappingCueAtNextReceivedStart()
    {
        var result = WebVttBuilder.Build([new(0, 2000, "First"), new(1000, 3000, "Second")]);
        Assert.Contains("00:00:00.000 --> 00:00:01.000", result);
        Assert.Contains("00:00:01.000 --> 00:00:03.000", result);
    }

    [Fact(DisplayName = nameof(ShortWindowWinsOverMinimumDurationTarget))]
    public void ShortWindowWinsOverMinimumDurationTarget()
    {
        var result = WebVttBuilder.Build([new(0, 200, "First"), new(200, 1000, "Second")]);
        Assert.Contains("00:00:00.000 --> 00:00:00.200", result);
        Assert.Contains("00:00:00.200 --> 00:00:01.000", result);
    }

    [Fact(DisplayName = nameof(ExtendsShortCueToMinimumWhenWindowAllows))]
    public void ExtendsShortCueToMinimumWhenWindowAllows()
        => Assert.Contains("00:00:00.000 --> 00:00:00.500", Build(0, 200, "First"));

    [Fact(DisplayName = nameof(LongTextInSixHundredMillisecondsStaysWhole))]
    public void LongTextInSixHundredMillisecondsStaysWhole()
    {
        var text = new string('a', 168);
        var result = WebVttBuilder.Build([new(0, 600, text), new(600, 1000, "Next")]);
        Assert.Equal(2, Cues(result).Length); Assert.Contains(text, result);
        Assert.Contains("00:00:00.000 --> 00:00:00.600", result);
    }

    [Fact(DisplayName = nameof(MergesEqualStartsIncludingZeroDurationInReceivedOrder))]
    public void MergesEqualStartsIncludingZeroDurationInReceivedOrder()
    {
        var result = WebVttBuilder.Build([new(1000, 1000, "First"), new(1000, 1200, "Second"), new(1000, 2000, "Third")]);
        Assert.Single(Cues(result)); Assert.Contains("First Second Third", result);
        Assert.Contains("00:00:01.000 --> 00:00:02.000", result);
    }

    [Fact(DisplayName = nameof(ZeroDurationStillReceivesAvailableWindow))]
    public void ZeroDurationStillReceivesAvailableWindow()
    {
        var result = WebVttBuilder.Build([new(0, 0, "First"), new(300, 300, "Next")]);
        Assert.Contains("00:00:00.000 --> 00:00:00.300", result);
        Assert.Contains("00:00:00.300 --> 00:00:00.800", result);
    }

    private static string Build(long start, long end, string text) => WebVttBuilder.Build([new(start, end, text)]);
    private static string[] Cues(string text) => text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
}
