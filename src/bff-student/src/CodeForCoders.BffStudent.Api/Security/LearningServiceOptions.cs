namespace CodeForCoders.BffStudent.Api.Security;

public sealed class LearningServiceOptions
{
    public const string SectionName = "Learning";
    public string BaseAddress { get; set; } = "http://localhost:5102/";
    public int TimeoutSeconds { get; set; } = 5;
}
