namespace CodeForCoders.Learning.Infra.Data.Configuration;

public static class LearningModules
{
    public const string Content = "Content";
    public const string Progress = "Progress";

    public static IReadOnlyList<string> All { get; } = [Content, Progress];
}
