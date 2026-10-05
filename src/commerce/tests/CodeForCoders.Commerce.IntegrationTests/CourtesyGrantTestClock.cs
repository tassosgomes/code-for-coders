namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyGrantTestClock : TimeProvider
{
    public static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-15T14:00:00Z");
    public DateTimeOffset Now { get; set; } = Start;
    public override DateTimeOffset GetUtcNow() => Now;
}
