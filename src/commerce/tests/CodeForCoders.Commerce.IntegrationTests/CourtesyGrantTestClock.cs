namespace CodeForCoders.Commerce.IntegrationTests;

public sealed class CourtesyGrantTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-15T14:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
