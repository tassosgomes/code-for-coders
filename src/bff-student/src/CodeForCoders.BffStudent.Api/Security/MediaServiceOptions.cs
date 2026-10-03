namespace CodeForCoders.BffStudent.Api.Security;

public sealed class MediaServiceOptions
{
    public const string SectionName = "Media";
    public string BaseAddress { get; set; } = "http://localhost:5103/";
    public int TimeoutSeconds { get; set; } = 5;
}
