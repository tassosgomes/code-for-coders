using System.Text;
using System.Text.Json;
using CodeForCoders.Media.Application.Common;
using CodeForCoders.Media.Domain.SeedWork;
using Xunit;

namespace CodeForCoders.Media.UnitTests;

public sealed class TranscriptionResultValidationTests
{
    [Fact]
    public void TrimsDiscardsEmptySegmentsAndOrdersByStart()
    {
        var content = Validate("""{"schemaVersion":1,"language":"pt-BR","durationMs":3000,"segments":[{"startMs":1000,"endMs":2000,"text":" second "},{"startMs":0,"endMs":1000,"text":" first "},{"startMs":2000,"endMs":2000,"text":"  "}]}""");
        using var document = JsonDocument.Parse(content.SegmentsJson);
        Assert.Equal("first", document.RootElement[0].GetProperty("text").GetString());
        Assert.Equal("second", document.RootElement[1].GetProperty("text").GetString());
        Assert.Equal(2, document.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RejectsUnknownSchema(int version)
        => Assert.Throws<EntityValidationException>(() => Validate($$"""{"schemaVersion":{{version}},"language":"pt-BR","durationMs":1,"segments":[]}"""));

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public void RejectsInvalidSegmentTimes(long start, long end)
        => Assert.Throws<EntityValidationException>(() => Validate($$"""{"schemaVersion":1,"language":"pt-BR","durationMs":3,"segments":[{"startMs":{{start}},"endMs":{{end}},"text":"text"}]}"""));

    [Fact]
    public void RejectsMoreThanTwentyThousandSegments()
    {
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, language = "pt-BR", durationMs = 1, segments = Enumerable.Range(0, 20001).Select(_ => new { startMs = 0, endMs = 1, text = " " }) });
        Assert.Throws<EntityValidationException>(() => Validate(json));
    }

    [Fact]
    public void RejectsOversizedJson()
        => Assert.Throws<EntityValidationException>(() => TranscriptionResultValidation.Validate(Guid.CreateVersion7(), Guid.CreateVersion7(), new byte[TranscriptionResultValidation.MaximumJsonBytes + 1]));

    [Fact]
    public void RejectsMalformedJsonWithoutEchoingContent()
    {
        var error = Assert.Throws<EntityValidationException>(() => Validate("sensitive transcript"));
        Assert.DoesNotContain("sensitive", error.Message);
    }

    [Fact]
    public void PreservesLanguageAndEqualSegmentEndpoints()
    {
        var content = Validate("""{"schemaVersion":1,"language":"en-US","durationMs":1,"segments":[{"startMs":0,"endMs":0,"text":"zero"}]}""");
        Assert.Equal("en-US", content.Language);
        Assert.Equal(1, content.DurationMs);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(9, 60)]
    [InlineData(10, 300)]
    [InlineData(119, 300)]
    [InlineData(120, 900)]
    public void PollCadenceFollowsElapsedSubmissionTime(int minutes, int seconds)
    {
        var submitted = DateTimeOffset.UtcNow;
        Assert.Equal(TimeSpan.FromSeconds(seconds), new TranscriptionOptions().PollDelay(submitted, submitted.AddMinutes(minutes)));
    }

    private static CodeForCoders.Media.Domain.Entities.TranscriptionContent Validate(string json)
        => TranscriptionResultValidation.Validate(Guid.CreateVersion7(), Guid.CreateVersion7(), Encoding.UTF8.GetBytes(json));
}
