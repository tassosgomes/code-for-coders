namespace CodeForCoders.Learning.IntegrationTests;

public sealed class LessonTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
