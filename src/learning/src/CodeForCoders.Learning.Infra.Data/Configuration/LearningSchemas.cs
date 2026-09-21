namespace CodeForCoders.Learning.Infra.Data.Configuration;

public static class LearningSchemas
{
    public const string Content = "content";
    public const string Progress = "progress";

    public static IReadOnlyList<string> All { get; } = [Content, Progress];
}
